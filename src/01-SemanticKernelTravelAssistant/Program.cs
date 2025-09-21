using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>();

IConfiguration configuration = builder.Build();

var endpoint = new Uri(configuration["ENDPOINT"] ?? "https://<your-endpoint>.openai.azure.com/");
var deploymentName = configuration["DEPLOYMENT_NAME"] ?? "gpt-5-chat";

var getToday = KernelFunctionFactory.CreateFromMethod(
    method: () => GetToday(),
    functionName: "get_today",
    description: "Returns today's date."
);

var getPreviousTravelsFunction = KernelFunctionFactory.CreateFromMethod(
    method: GetPreviousTraveDetails(),
    functionName: "get_previous_travels",
    description: "Returns a list of previous travels."
);

var getFlightsFunction = KernelFunctionFactory.CreateFromMethod(
    method: GetFlights(),
    functionName: "get_flights",
    description: @"Finds a list of flights.
        You need to use the airport codes in IATA format '<airport_code>'."
);

var getHotelFunction = KernelFunctionFactory.CreateFromMethod(
    method: GetHotels(),
    functionName: "get_hotels",
    description: @"Finds a list of hotels in a specific city."
);

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromFunctions("GetToday", "Date retrieval", [getToday]);
kernelBuilder.Plugins.AddFromFunctions("GetPreviousTravels", "Previous travel retrieval", [getPreviousTravelsFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetFlights", "Flight retrieval", [getFlightsFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetHotels", "Hotel retrieval", [getHotelFunction]);

var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);

var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Kernel = kernel,
    Instructions = @"
        You are travel assistant helping users with their travel plans.
        User is Mr. John Doe. His travel preferences are as follows:
        - Home city is Helsinki and he departs from HEL airport.
        - He prefers direct flights and economy class accommodations.

        Use the available functions to get today's date, look up previous travels,
        find flights, and find hotels.
        When you have gathered all the necessary information to make a booking,
        present the options to the user and ask for their confirmation.
        Use the following format to present options:
        For flights:
        Here is my recommendation for flight from <departure_city> (<departure_airport_code>) to <arrival_city> (<arrival_airport_code>:
        <Airline> <Flight Number> Departure: <departure_week_day> <departure_time> Arrival: <arrival_week_day> <arrival_time> Duration: <duration> Class: <class> Price: <price>
        For hotels:
        Here is my recommendation for hotel in <city>:
        <Hotel Name> Check-In: <Check-In Date> Check-Out: <Check-Out Date> Number of Guests: <Number of Guests> Price per Night: <Price per Night>
        Use markdown tables to present the options in a clear and organized manner.

        After you have received confirmation, then
        you can let user know that you're finalizing the travel arrangements
        and that they will get a confirmation email shortly including
        all the travel documents and calendar invites.
        Provide relevant travel greetings at the end.",
    Arguments = new KernelArguments(new PromptExecutionSettings()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    })
};

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();
var isInitialMessage = true;
var initialMessage = @"
I need to travel again to Stockholm next week Wednesday for 3 days.
Book me my usual hotel and rent a car as well.
Also, find something fun to do for Thursday evening.
";

while (true)
{
    string input;
    if (isInitialMessage)
    {
        Console.WriteLine($"> {initialMessage}");
        isInitialMessage = false;
        input = initialMessage;
    }
    else
    {
        Console.Write("> ");
        input = Console.ReadLine() ?? string.Empty;
    }

    chatMessages.Add(new ChatMessageContent(AuthorRole.User, input));

    Console.WriteLine("Response: ");

    string chatResponse = string.Empty;

    await foreach (AgentResponseItem<StreamingChatMessageContent> response in agent.InvokeStreamingAsync(chatMessages))
    {
        chatResponse += response.Message;
        Console.Write(response.Message);
    }

    chatMessages.Add(new ChatMessageContent(AuthorRole.Assistant, chatResponse));

    Console.WriteLine();
    Console.WriteLine();
}

static DateTime GetToday()
{
    Console.WriteLine("<Function-GetToday: Fetching today's date>");
    return DateTime.Now;
}

static Func<int, string> GetPreviousTraveDetails()
{
    return (int travelDetailsFromLastDays) =>
    {
        Console.WriteLine($"<Function-GetPreviousTraveDetails: Fetching previous travels from last {travelDetailsFromLastDays} days>");
        return $@"
        Here are some of your previous travels from last {travelDetailsFromLastDays} days:

        1. Stockholm, Sweden,  - {DateTime.Now.AddDays(-10):yyyy-MM-dd} to {DateTime.Now.AddDays(-7):yyyy-MM-dd}
          - Airport: Stockholm Arlanda Airport (ARN)
          - Airline: Contoso Air, Flight Number: CA123, Class: Economy
          - Hotel: Contoso Hotel
        2. Stockholm, Sweden - {DateTime.Now.AddDays(-20):yyyy-MM-dd} to {DateTime.Now.AddDays(-15):yyyy-MM-dd}
          - Airport: Stockholm Arlanda Airport (ARN)
          - Airline: Contoso Air, Flight Number: CA123, Class: Economy
          - Hotel: Contoso Hotel
        3. Tokyo, Japan - {DateTime.Now.AddDays(-30):yyyy-MM-dd} to {DateTime.Now.AddDays(-25):yyyy-MM-dd}
          - Airport: Narita International Airport (NRT)
          - Airline: Fabrikam Air, Flight Number: FA456, Class: Economy
          - Hotel: Fabrikam Hotel
    ";
    };
}

static Func<string, string, DateTime, DateTime, string, string> GetFlights()
{
    return (string departureAirportCode, string arriveAirportCode,
                 DateTime departureTime, DateTime returnTime,
                 string travelClass) =>
    {
        Console.WriteLine($"<Function-GetFlights: Fetching flights from {departureAirportCode} to {arriveAirportCode}>");
        Console.WriteLine($"<Function-GetFlights: Departure time {departureTime}, Return time {returnTime}, Class {travelClass}>");
        return $@"
            Here are some flight options from {departureAirportCode} to {arriveAirportCode}:

            | Airline       | Flight Number | Departure Time     | Arrival Time       | Duration | Class        | Price   |
            |---------------|---------------|--------------------|--------------------|----------|--------------|---------|
            | Contoso Air   | CA123         | {departureTime:yyyy-MM-dd HH:mm} | {departureTime.AddHours(3):yyyy-MM-dd HH:mm} | 3h       | {travelClass} | $300    |
            | Fabrikam Air  | FA456         | {departureTime.AddHours(1):yyyy-MM-dd HH:mm} | {departureTime.AddHours(4):yyyy-MM-dd HH:mm} | 3h       | {travelClass} | $320    |

            Here are some return flight options from {arriveAirportCode} to {departureAirportCode}:

            | Airline       | Flight Number | Departure Time     | Arrival Time       | Duration | Class        | Price               |---------------|---------------|--------------------|--------------------|----------|--------------|---------|
            |---------------|---------------|--------------------|--------------------|----------|--------------|---------|
            | Contoso Air   | CA789         | {returnTime:yyyy-MM-dd HH:mm} | {returnTime.AddHours(3):yyyy-MM-dd HH:mm} | 3h       | {travelClass} | $300    |  
            | Fabrikam Air  | FA101         | {returnTime.AddHours(1):yyyy-MM-dd HH:mm} | {returnTime.AddHours(4):yyyy-MM-dd HH:mm} | 3h       | {travelClass} | $320    |
            ";
    };
}

static Func<string, DateTime, DateTime, int, string> GetHotels()
{
    return (string city, DateTime checkInDate, DateTime checkOutDate, int numberOfGuests) =>
    {
        Console.WriteLine($"<Function-GetHotels: Fetching hotels in {city}>");
        Console.WriteLine($"<Function-GetHotels: Check-In Date {checkInDate}, Check-Out Date {checkOutDate}, Number of Guests {numberOfGuests}>");
        return $@"
            Here are some hotel options in {city}:
            | Hotel Name       | Check-In Date     | Check-Out Date    | Number of Guests | Price per Night |
            |------------------|-------------------|-------------------|------------------|-----------------|
            | Contoso Hotel    | {checkInDate:yyyy-MM-dd} | {checkOutDate:yyyy-MM-dd} | {numberOfGuests}              | $150            |
            | Fabrikam Suites  | {checkInDate:yyyy-MM-dd} | {checkOutDate:yyyy-MM-dd} | {numberOfGuests}              | $180            |
            ";
    };
}
