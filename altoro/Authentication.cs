using System.Net;
using Microsoft.Extensions.Logging;

public interface IAuthentication {
    Task<(bool successful, CookieContainer cookieContainer)> LoginAsync(string username, string password);
    void Logout();
}

public class Auth : IAuthentication {
    private readonly ILogger<Auth> _logger;

    private CookieContainer _cookieContainer;

    public Auth(ILogger<Auth> logger) {
        _logger = logger;
        _cookieContainer = new CookieContainer();
    }
    /***
        Logs in the user using the web controller route and not the api
        route. It allows us to call more of the methods available.
    **/
    public async Task<(bool successful, CookieContainer cookieContainer)> LoginAsync(
        string username,
        string password
    )
    {
        var handler = new HttpClientHandler() {
            CookieContainer = _cookieContainer,
        };
        var httpClient = new HttpClient(handler);

        var uri = "http://demo.testfire.net/doLogin";
        
        var form = new Dictionary<string, string>();
        form.Add("uid", username);
        form.Add("passw", password);
        var httpContent = new FormUrlEncodedContent(form);

        var request = new HttpRequestMessage(HttpMethod.Post, uri) {
            Content = httpContent,
        };
        _logger.LogInformation("LoginAsync Sending request for {Username}", username);
        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.OK) {
            // Where you're redirected to if the login was successful
            var successLocation = "http://demo.testfire.net/bank/main.jsp";
            if(response.RequestMessage?.RequestUri?.ToString() == successLocation) {
                _logger.LogInformation("LoginAsync Successfully logged in");
                return (true, _cookieContainer);
            }
            _logger.LogInformation("LoginAsync Invalid Credentials");
            return (false, _cookieContainer);
        }

        _logger.LogError("LoginAsync Received unexpected HTTP Status {StatusCode} - {StatusMessage}", response.StatusCode, response.ReasonPhrase);
        return (false, _cookieContainer);
    }

    public void Logout() {
        // Clear the cookies
        _cookieContainer = new CookieContainer();
    }
}