using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HaulTech.Public.CSP;

namespace CSP_Integration_Demo
{

  class Program
  {

    #region Demo variables
    // The base URLs for the CSP API and CSP Authorisation endpoints
    static readonly string _apiUrl = "";
    static readonly string _authorisationUrl = "";

    // There credentials are here simply as an example, DO NOT ever store credentials in
    // plain text in a production environment.
    static readonly string _username = "";
    static readonly string _password = "";
    #endregion

    static async Task Main(string[] args)
    {
      #region Obtain an OAuth token
      var httpClient = new HttpClient(); // This is used throughout to inject into the CSP services
      var authClient = new AuthClient(httpClient)
      {
        BaseUrl = _authorisationUrl,
        Username = _username,
        Password = _password
      };
      try
      {
        // Retreive a token then add to the base HttpClient using the Bearer scheme
        var oAuthToken = await authClient.GetTokenAsync();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {oAuthToken.access_token}");
      }
      catch (Exception exception)
      {
        LogException(exception);
        throw;
      }
      #endregion
      #region  Resolve the first Account
      // For the purpose of the demo, we'll resolve the first Account our user has
      // permission to access.
      var accountClient = new AccountClient(httpClient) { BaseUrl = _apiUrl };
      ExtendedAccount account;
      try
      {
        var accounts = await accountClient.AllAccountsAsync();
        account = accounts.FirstOrDefault();
        if (account == null)
        {
          Console.WriteLine("Unable to resolve any Accounts.");
        }
      }
      catch (Exception exception)
      {
        LogException(exception);
        throw;
      }
      #endregion
      #region Resolve Service Levels for Collection and Delivery
      var serviceClient = new ServiceClient(httpClient) { BaseUrl = _apiUrl };
      List<ServiceMatrix> serviceMatrix;
      int? firstServiceMatrixId;
      try
      {
        serviceMatrix = await serviceClient.MatrixAsync();
        firstServiceMatrixId = serviceMatrix.FirstOrDefault()?.ServiceLevelId;
      }
      catch (Exception exception)
      {
        LogException(exception);
        throw;
      }
      if (firstServiceMatrixId == null || firstServiceMatrixId <= 0)
      {
        Console.WriteLine("Unable to resolve Service Matrix.");
        return;
      }
      // TODO: Awaiting NuGet improvements to handle `ServiceMatrix.Type` as an enum
      var collectionServiceLevel = serviceMatrix.FirstOrDefault(m => m.ServiceLevelId == firstServiceMatrixId && m.Type == "C");
      var deliveryServiceLevel = serviceMatrix.FirstOrDefault(m => m.ServiceLevelId == firstServiceMatrixId && m.Type == "D");
      if (collectionServiceLevel == null || deliveryServiceLevel == null)
      {
        Console.WriteLine("Unable to resolve Service Levels.");
        return;
      }
      #endregion
      #region Add a pending Job
      var pendingJob = new PortalJob
      {
        // These are the minimum fields necessary to add a job to the CSP
        AccountId = account.AccountId ?? 0,
        ServiceLevelId = collectionServiceLevel.ServiceLevelId ?? 0,
        CollectionDate = Today(),
        CollectionTime = collectionServiceLevel.ServiceTimeId ?? 0,
        CollectionStartTime = collectionServiceLevel.StartTime ?? 0,
        CollectionEndTime = collectionServiceLevel.EndTime ?? 0,
        CollectionAddress1 = "CSP NuGet Demo Collection",
        DeliveryDate = Today((double)collectionServiceLevel.AdvanceDays),
        DeliveryTime = deliveryServiceLevel.ServiceTimeId ?? 0,
        DeliveryStartTime = deliveryServiceLevel.StartTime ?? 0,
        DeliveryEndTime = deliveryServiceLevel.EndTime ?? 0,
        DeliveryAddress1 = "CSP NuGet Demo Delivery",
        Weight = 0
      };
      var jobClient = new JobClient(httpClient) { BaseUrl = _apiUrl };
      int? pendingId;
      try
      {
        // TODO: Awaiting NuGet improvements to return an `int` rather than `string`
        int.TryParse(await jobClient.AddAsync(pendingJob), out var result);
        if (result > 0) { pendingId = result; }
        else
        {
          Console.WriteLine("Adding a pending job failed.");
          Console.WriteLine($"result={result}");
          return;
        }
      }
      catch (Exception exception)
      {
        LogException(exception);
        throw;
      }
      #endregion
      #region  Confirm a pending Job
      try
      {
        var jobResult = await jobClient.ConfirmAsync(pendingId ?? 0);
        Console.WriteLine(jobResult);
      }
      catch (Exception exception)
      {
        LogException(exception);
        throw;
      }
      #endregion
    }

    /// <summary>
    /// A method to normalise how to handle exceptions.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    private static void LogException(Exception exception)
    {
      // Grab the current console colour
      var previousForeground = Console.ForegroundColor;
      // Update the console colour
      Console.ForegroundColor = ConsoleColor.Red;
      // Log the exception
      Console.WriteLine($"An exception occurred: {exception.Message}");
      if (exception.InnerException != null)
      {
        Console.WriteLine($"Inner Exception: {exception.InnerException.Message}");
      }
      // Reset the console colours to previous state
      Console.ForegroundColor = previousForeground;
    }

    /// <summary>
    /// Dummy method to create dates, nothing like what you would do in a real integration.
    /// </summary>
    /// <param name="advanceDays"></param>
    /// <returns>An integrer representation of a date in yyyyMMdd format.</returns>
    private static int Today(double? advanceDays = 0)
    {
      // TODO: Awaiting NuGet improvements to calcuate `DeliveryDate` as
      // this property is bound by the rules defined in `ServiceMatrix.PermittedDays`
      var now = DateTime.Now;
      return int.Parse(now.AddDays(advanceDays ?? 0).ToString("yyyyMMdd"));
    }

  }

}
