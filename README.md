# Durable Functions demo app for e-signing documents

This app was made for the Virtual Global Azure 2020 event.
It has a web front-end built with ASP.NET Core and Razor Pages,
as well as a Durable Functions app running the e-signing workflow.
The app uses an SQL database to store the signing requests.

Note the app was made for demo purposes and is not ready for production use.
Additional authentication and authorization would be required to verify the signers.
Additionally, the PDF library used (IronPDF) requires a paid license for production use.
