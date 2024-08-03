# Durable Functions demo app for e-signing documents

This app was originally made for the Virtual Global Azure 2020 event and updated for Techorama 2024.
It has a web front-end built with ASP.NET Core and Razor Pages,
as well as a Durable Functions app running the e-signing workflow.
The app uses an SQL database to store the signing requests.

Note the app was made for demo purposes and is not ready for production use.
Additional authentication and authorization would be required to verify the signers.
The PDFs are also not actually digitally signed, a page is added with the signers' email addresses.

## Local setup

Ensure you have the following installed:

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
- [Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer/)
  - This is only needed to create the needed Storage container in the emulator. You can use other tools as well.
- An SQL database server
  - The app has been tested with SQL Server Express LocalDB, which is included with Visual Studio 2022 and can be included in .NET desktop development workload or through Individual components

Rename _local.settings.sample.json_ to _local.settings.json_ in the Workflows project.
Update the database connection string to match your environment both there and in appsettings.Development.json of the Web project.

Add your SendGrid API key and sender address to local.settings.json in the Workflows project.

Run Azurite and Azure Storage Explorer.
In Storage Explorer, go to: Local & Attached -> Storage Accounts -> (Emulator - Default Ports) (Key) -> Blob Containers.
Right-click on Blob Containers and click Create Blob Container.
Give the container the name _esigning_.

Open Package Manager Console in Visual Studio or a command line window in the solution root folder.
There run the following commands to install EF tools and create the database tables:

```
dotnet tool restore
dotnet ef database update -s Joonasw.ElectronicSigningDemo.Web -p Joonasw.ElectronicSigningDemo.Data
```

You can run the app using only the command line, or you can use an IDE like Visual Studio or Visual Studio Code.

Run the Workflows project first and ensure there are no errors in the output.
Then run the Web project, and you should be able to send a document for e-signing.
