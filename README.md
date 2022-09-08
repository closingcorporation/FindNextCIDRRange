# Intro

Hello :wave:

This repo is a fork from the original creator [gamullen](https://github.com/gamullen/FindNextCIDRRange)

The fork aims to add some functionality to the app, as well as integrate it with some CI/CD, terraform and other bits and bobs.

## What this Function App does

This repo hosts all the code and the mechanisms to deploy a Linux Azure Function App into the Libre DevOps tenant, with this function app acting as a method of getting the next CIDR range in a VNet.  Please check and the [original documentation and blog post](https://techcommunity.microsoft.com/t5/azure-networking-blog/programmatically-find-next-available-cidr-for-subnet/ba-p/3266016) for information on the original app. 

This repo:

- Build function app and needed resources using terraform
- Deploy code via GitHub Actions

The function itself is an HTTP function, which, will only run on request. Please note, the function app does require reader permissions over the virtual network.

**Worth a note, that this won't work out the box for you, you are expected to use your own intuition or reach out for help :smile:**

There are 1 functions:
- `Get-Cidir` 

You can query the API by feeding it the parameters in the following format via a HTTP GET request:

`https://{{pathToFunctionApp}}?subscriptionId={{subscriptionId}}&resourceGroupName={{resourceGroupName}}&virtualNetworkName={{virtualNetworkName}}&cidr={{cidr}}`

So, for example:

`https://fnc-ldo-euw-dev-01.azurewebsites.net/api/getcidr?subscriptionId=09d383ee-8ed0-4374-ad9f-3344cabc323b&resourceGroupName=rg-ldo-euw-dev-build&virtualNetworkName=vnet-ldo-euw-dev-01&cidr=26`

With example output:

```json
{
  "name": "vnet-ldo-euw-dev-01",
  "id": "/subscriptions/09d383ee-8ed0-4374-ad9f-3344cabc323b/resourceGroups/rg-ldo-euw-dev-build/providers/Microsoft.Network/virtualNetworks/vnet-ldo-euw-dev-01",
  "type": "Microsoft.Network/virtualNetworks",
  "location": "westeurope",
  "proposedCIDR": "10.0.0.0/26"
}
```

_Please note, my function app will be taken down by Terraform regularly, chances are if you try to test query it, it will fail_

## Building the environment

At the time of writing, this project only supports Azure DevOps continuous integration and is set up to deploy using some expected items in the Libre DevOps Azure DevOps instance.

You can freely use the modules used to deploy these resources as well as the pipeline templates, but setting up the bits in between will be up to you.


## Terraform Build

### Disclaimer

This solutions is _not_ turnkey and won't take into considerations the perimeter network and identity policies you will have in your tenant.  The automation is provided as-is for a quick setup and is not intended for production deployments. Please be sure to conduct due diligence when using.

The build will deploy:
- 1x Resource Group
- 1x Linux Function app on Consumption Service Plan with Dotnet 6.0 Application Stack (up to date with the v3 Azurerm provider changes in terraform)
- 1x Storage Account, Hot access tier
- 1x Blob container with blob (anonymous access) for the URLs

### How to use

To use the terraform bui.d, you will need to _already have_ a virtual network created, as well as knowing the name of the resource group is in, as well as having IAM access to read and assign permissions to this resource (e.g Owner or Reader + User Access Administrator) 

Next, simply edit the `run-terraform.sh` script with the variables placed at the top of the script, which will then be passed to terraform.  The shell script **requires** the `azure-cli`, `curl`, and `jq` to work. It will check if they are installed. in its default state, as it is using it as its authentication method to terraform.  

Again, you will need various IAM access to run this terraform under your account, but you are free to adapt it to run under a service principal or managed identity :smile:

An example is something like so

```shell

```
