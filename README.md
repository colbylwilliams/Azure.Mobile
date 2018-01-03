# Azure.Mobile

**[Azure.Mobile](https://aka.ms/mobile) is a framework for rapidly creating iOS and android apps with modern, highly-scalable backends on Azure.**

Azure.Mobile has two simple objectives:

1. Enable developers to create, configure, deploy all necessary backend services fast — ideally under 10 minutes with only a few clicks
2. Provide native iOS and android SDKs with delightful APIs to interact with the services


## What's included?

It includes one-click deployment templates and native client SDKs for the following:

- Database
- Blob/File Storage
- Authentication
- Push Notifications
- Serverless Functions
- Client/Server Analytics



## How do I use it?

### 1. Azure Account

To use Azure.Mobile, you'll need an Azure account.  If you already have one, make sure you’re [logged in](https://portal.azure.com) and move to the next step.

If you don't have an Azure account, [sign up for a Azure free account][azure-free] before moving to the next step.


### 2. Deploy Azure Services

Deploying the Azure resources is as simple as clicking the link below then filling out the form per the instructions under the link:

[![Deploy to Azure][azure-deploy-button]][azure-deploy]


#### Template Form

- **Subscription:** Choose which Azure subscription you want to use to deploy the backend.  If you only have one choice, or you don't see this option at all, don't sweat it.
- **Resource group:** Unless you have an existing Resource group that you know you want to use, select __Create new__ and provide a name for the new group.  _(a resource group is essentially a parent folder to deploy the new database, app service, etc. to)_
- **Location:** Select the region to deploy the new resources. You want to choose a region that best describes your location (or your users location).
- **Web Site Name:** Provide a name for your app.  This can be the same name as your Resource group, and will be used as the subdomain for your service endpoint.  For example, if you used `superawesome`, your serverless app would live at `superawesome.azurewebsites.net`.
- **Function Language:** The template will deploy a serverless app with a few boilerplate functions.  This is the programming language those functions will be written in.  Choose the language you're most comfortable with.


#### Agree & Purchase

Read and agree to the _TERMS AND CONDITIONS_, then click **Purchase**.


### 3. Configure iOS/Android app


- [iOS SDK][azure-ios]
- [Android SDK][azure-android]


## How is this different than Azure Mobile Apps (formally Azure App Services)?

_coming soon..._



## What is the price/cost?

Most of these services have a generous free tier. [Sign up for a Azure free account][azure-free] to get $200 credit.



# About

- Created by [Colby Williams](https://github.com/colbylwilliams)

## Contributing

This is very much a work in progress, and contributions are absolutely welcome.  Feel free to file issues and pull requests on the repo and we'll address them as time permits.

## License

Licensed under the MIT License (MIT). See [LICENSE](LICENSE) for details.



[azure-ios]:https://aka.ms/azureios
[azure-android]:https://aka.ms/azureandroid

[cosmos]:https://azure.microsoft.com/en-us/services/cosmos-db

[azure-deploy]:https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fcolbylwilliams%2FAzure.Mobile%2Fmaster%2Fazuredeploy.json
[azure-deploy-button]:https://azuredeploy.net/deploybutton.svg

[azure-visualize]:http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2FAzure.Mobile%2Fmaster%2Fazuredeploy.json
[azure-visualize-button]:http://armviz.io/visualizebutton.png


[azure-free]:https://azure.microsoft.com/en-us/free/