// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*
 MIT License
Copyright (c) 2021 Gary L. Mullen-Schultz
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Network;
using System.Net;
using System.Collections.Generic;
using System.Collections;

namespace FindNextCIDR
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string subscriptionId = req.Query["subscriptionId"];
            string virtualNetworkName = req.Query["virtualNetworkName"];
            string resourceGroupName = req.Query["resourceGroupName"];
            string cidrString = req.Query["cidr"];
            byte cidr = 0;

            Exception error = null;
            string errorMessage = null;
            bool success = false;
            bool foundBadSubnetInCandidateCIDR = false;
            string foundSubnet = null;

            try
            {
                // Validate the input params
                errorMessage = validateInput(subscriptionId, virtualNetworkName, resourceGroupName, cidrString);
                if (null == errorMessage)
                {
                    // Make sure the CIDR is valid
                    if (validateCIDR(cidrString))
                    {
                        cidr = Byte.Parse(cidrString);
                        VirtualNetwork vNet = null;

                        // Get a client for the SDK calls
                        var armClient = new ArmClient(subscriptionId, new DefaultAzureCredential());
                        Azure.Pageable<VirtualNetwork> vNets = armClient.GetDefaultSubscription().GetVirtualNetworks();

                        // For each VNet find ours!
                        foreach (VirtualNetwork vNet2 in vNets)
                        {
                            // Find the desired VNet
                            if (virtualNetworkName.Equals(vNet2.Data.Name) && resourceGroupName.Equals(vNet2.Id.ResourceGroupName)) {   
                                vNet = vNet2;
                            }
                        }

                        if (null == vNet)
                        {
                            errorMessage = "Virtual network " + virtualNetworkName + " not found.";
                        }
                        else
                        {
                            Hashtable vNetCIDRs = new Hashtable();

                            foreach (string ip in vNet.Data.AddressSpace.AddressPrefixes)
                            {
                                IPNetwork vNetCIDR = IPNetwork.Parse(ip);
                                vNetCIDRs.Add(vNetCIDR.GetHashCode(), vNetCIDR);
                            }

                            //Go though every address space in the specified VNet
                            foreach (IPNetwork candidateCIDR in vNetCIDRs.Values)
                            {
                                // Get a list of all CIDRs that could possibly fit into the given address space with the CIDR range requested
                                IPNetworkCollection candidateSubnets = candidateCIDR.Subnet(cidr);
                                IList usedSubnets = new ArrayList();

                                // Get every Azure subnet in the VNet
                                SubnetCollection usedSubnetsAzure = vNet.GetSubnets();

                                // Convert into IPNetwork object list
                                foreach (Subnet usedSubnet in usedSubnetsAzure)
                                {
                                    usedSubnets.Add(IPNetwork.Parse(usedSubnet.Data.AddressPrefix));
                                }

                                // Check each candidate subnet CIDR
                                foreach (IPNetwork candidateSubnet in candidateSubnets)
                                {
                                    if (!success)
                                    {
                                        foundBadSubnetInCandidateCIDR = false;
                                        // Go through each Azure subnet in VNet, check against candidate
                                        foreach (IPNetwork usedSubnet in usedSubnets)
                                        {
                                            if (!foundBadSubnetInCandidateCIDR && !(usedSubnet.Overlap(candidateSubnet)))
                                            {
                                                success = true;
                                                foundSubnet = candidateSubnet.ToString();
                                            }
                                            else
                                            {
                                                foundBadSubnetInCandidateCIDR = true;
                                            }
                                        }

                                        Console.WriteLine("candidate CIDR: " + candidateCIDR + "success = " + success + ", badsubnet = " + foundBadSubnetInCandidateCIDR);
                                        if (foundBadSubnetInCandidateCIDR)
                                        {
                                            success = false;
                                            foundBadSubnetInCandidateCIDR = false;
                                        }
                                    }
                                }
                            } 

                            if (!success)
                            {
                                errorMessage = "VNet " + resourceGroupName + "/" + virtualNetworkName + " cannot accept a subnet of size " + cidr;
                            }
                        }
                    } else
                    {
                        errorMessage = "Invalid CIDR: " + cidrString;
                    }
                } else
                {
                    errorMessage = "Invalid input: " + errorMessage;
                }
            }
            catch (Exception e)
            {
                error = e;
                // empty code var will signal error
            }

            ObjectResult result = null;
            if ((null == errorMessage) && success)
            {
                result = new OkObjectResult("Available CIDR block for CIDR size " + cidr + " = " + foundSubnet);
            }
            else
            {
                if (null != error)
                {
                    errorMessage = error.Message;
                }

                result = new BadRequestObjectResult(errorMessage);
            }

            return result;
        }

        private static string validateInput(string subscriptionId, string virtualNetworkName, string resourceGroupName, string cidrString)
        {
            string errorMessage = null;

            if (null == subscriptionId)
            {
                errorMessage = "subscriptionId is null";
            }
            else if (null == virtualNetworkName)
            {
                errorMessage = "virtualNetworkName is null";
            }
            else if (null == resourceGroupName)
            {
                errorMessage = "resourceGroupName is null";
            }
            else if (null == cidrString)
            {
                errorMessage = "cidr is null";
            }

            return errorMessage;
        }

        private static bool validateCIDR(string inCIDR)
        {
            bool isGood = false;

            try
            {
                byte cidr = Byte.Parse(inCIDR);
                isGood = (2 <= cidr && 29 >= cidr);
            } catch
            {

            }

            return isGood;
        }
    }
}