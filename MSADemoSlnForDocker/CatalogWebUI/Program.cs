using CatalogWebUI;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Base URL points to CatalogAPI exposed port from host browser perspective
var catalogApiUrl = builder.Configuration["CatalogApiUrl"] ?? "http://localhost:5139/";

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(catalogApiUrl) });
builder.Services.AddScoped<CatalogHttpService>();

await builder.Build().RunAsync();
