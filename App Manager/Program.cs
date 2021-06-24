using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using App_Manager.Controller;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;

using Newtonsoft.Json;
namespace App_Manager
{
  class Program
  {
    private static string mode = "Template"; // Template else Registration?
    
    static void Main(string[] args)
    {
      var config = LoadAppSettings(); 
      
      if (config != null)
      {
        string userLogin = config["userLogin"];
        string appName = config["appName"];        
        string templateID = config["templateID"];
        string customRoleDefinitionId = config["customRoleDefinitionId"];
        HttpClientController clientController = new HttpClientController();
        clientController.InitializeUser(config["clientId"], config["tenantId"]);
        
        string appID = String.Empty;
        string principalID = String.Empty;
        Model.Application app = null;
        if (mode == "Template")
        {         
          Model.ApplicationServicePrincipal appSvcPcpl = clientController.InstantiateAppTemplate(templateID, appName).Result;
          app = appSvcPcpl.Application;
          appID = appSvcPcpl.ServicePrincipal.AppId;
          principalID = appSvcPcpl.ServicePrincipal.Id;
          Console.WriteLine("Enterprise App created with AppID {0} Service Principal ID {1} and name {2} based on ap template {3}", appID, principalID, appName, templateID);
        }
        else
        {       
          app = clientController.CreateApplication(appName).Result;
          appID = app.AppId;
          Console.WriteLine("App created with AppID {0} and name {1}", appID, appName);

          principalID = clientController.CreateServicePrincipal(appID).Result;
          Console.WriteLine("ServicePrincipal created created for App with AppID {0} and name {1}", appID, appName);
        }
       
        string roleAssgnmID = clientController.AddUnifiedRoleAssignment(customRoleDefinitionId, principalID, userLogin).Result;
        Console.WriteLine("Role Id {0} assigned to user {1} for App with name {2}", customRoleDefinitionId, userLogin, appName);
        string roleAssgnm2ID = clientController.AddUnifiedRoleAssignment(customRoleDefinitionId, app.Id, userLogin).Result;
        Console.WriteLine("Role Id {0} assigned to user {1} for Service Principal with name {2}", customRoleDefinitionId, userLogin, appName);

        bool claimsPolicyAssgnd = clientController.AssignClaimsMappingPolicy(principalID).Result;
        if (claimsPolicyAssgnd)
        {
          Console.WriteLine("Claims Policy assigned to Service Principal with ID {0} and name {1}", principalID, appName);
        }

        bool capPolicyAssgnd = clientController.AssignConditionalAccessPolicy(appID).Result;
        if (capPolicyAssgnd)
        {
          Console.WriteLine("Conditional Access Policy assigned to App with AppID {0} and name {1}", appID, appName);
        }
        Console.ReadLine();
      }
    }

    private static IConfigurationRoot LoadAppSettings()
    {
      try
      {
        string currentPath = System.IO.Directory.GetCurrentDirectory();
        var config = new ConfigurationBuilder()
                        .SetBasePath(currentPath)
                        .AddJsonFile("appsettings.json", false, true)
                        .Build();

        if (string.IsNullOrEmpty(config["clientId"]) ||            
            string.IsNullOrEmpty(config["tenantId"]))
        {
          return null;
        }

        return config;
      }
      catch (System.IO.FileNotFoundException)
      {
        return null;
      }
    }

    
    static void Main_alt(string[] args)
    {
      var config = LoadAppSettings();

      if (config != null)
      {
        string userLogin = config["userLogin"];
        string appName = config["appName"];
        string templateID = config["templateID"];
        string customRoleDefinitionId = config["customRoleDefinitionId"];
        string authority = $"https://login.microsoftonline.com/{config["tenantId"]}/v2.0";
        GraphController controller = new GraphController();
        controller.Initialize(config["clientId"], authority, config["clientSecret"]);
        controller.InitializeUser(config["clientId"], "", config["tenantId"]);

        string appID = String.Empty;
        string principalID = String.Empty;
        Model.Application app = null;
        if (mode == "Template")
        {
          var appSvcPcpl = controller.InstantiateAppTemplate(templateID, appName).Result;
          app = new Model.Application
          {
            AppId = appSvcPcpl.Application.AppId,
            Id = appSvcPcpl.Application.Id
          };
          appID = appSvcPcpl.ServicePrincipal.AppId;
          principalID = appSvcPcpl.ServicePrincipal.Id;
          Console.WriteLine("Enterprise App created with AppID {0} Service Principal ID {1} and name {2} based on ap template {3}", appID, principalID, appName, templateID);
        }
        else
        {
          app = controller.CreateApplication(appName).Result;
          appID = app.AppId;
          Console.WriteLine("App created with AppID {0} and name {1}", appID, appName);

          principalID = controller.CreateServicePrincipal(appID).Result;
          Console.WriteLine("ServicePrincipal created created for App with AppID {0} and name {1}", appID, appName);
        }

        string scope = "Mail.Read";
        var permID = controller.AddDelegatedPermission(principalID, scope).Result;
        Console.WriteLine("Delegated permission {0} added to App with ID {1} and name {2}", scope, appID, appName);
        scope = "Sites.Read.All";
        permID = controller.AddApplicationPermission(principalID, scope).Result;
        Console.WriteLine("Application permission {0} added to App with AppID {1} and name {2}", scope, appID, appName);
        string appSecret = controller.AddApplicationSecret(app.Id, 365).Result;
        Console.WriteLine("Secret {0} added to App with AppID {1} and name {2}", appSecret, appID, appName);
        string roleName = "Reviewer";
        string updatedAppId = controller.AddAppRole(app.Id, roleName).Result;
        Console.WriteLine("Role {0} added to App with ID {1} and name {2}", roleName, updatedAppId, appName);
        string roleAssgnmID = controller.AddAppRoleAssignment(updatedAppId, principalID, userLogin).Result;
        Console.WriteLine("Role {0} assigned to user {1} for App with name {2}", roleName, userLogin, appName);
        
      }
    }
  }
}
