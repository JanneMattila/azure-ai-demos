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
    method: GetPreviousTravelDetails(),
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

var getCarRentalsFunction = KernelFunctionFactory.CreateFromMethod(
    method: GetCarRentals(),
    functionName: "get_car_rentals",
    description: @"Finds a list of car rentals in a specific city."
);

var getActivitiesFunction = KernelFunctionFactory.CreateFromMethod(
    method: GetActivities(),
    functionName: "get_activities",
    description: @"Finds a list of activities in a specific city."
);

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromFunctions("GetToday", "Date retrieval", [getToday]);
kernelBuilder.Plugins.AddFromFunctions("GetPreviousTravels", "Previous travel retrieval", [getPreviousTravelsFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetFlights", "Flight retrieval", [getFlightsFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetHotels", "Hotel retrieval", [getHotelFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetCarRentals", "Car rental retrieval", [getCarRentalsFunction]);
kernelBuilder.Plugins.AddFromFunctions("GetActivities", "Activity retrieval", [getActivitiesFunction]);

var azureClient = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, azureClient);

var kernel = kernelBuilder.Build();

var agent = new ChatCompletionAgent
{
    Kernel = kernel,
    Instructions = @"
        You are friendly travel assistant helping users with their travel plans.
        User is Mr. John Doe. His travel preferences are as follows:
        - Home city is Helsinki and he departs from HEL airport.
        - He prefers direct flights and Economy class accommodations.

        Do not mention that you're using tools to help you.
        When calculating travel dates, include both the departure and return dates
        (e.g., if departing on a Monday and returning on Wednesday, that counts as 3 days)
        but remember that hotel stays are calculated by stayed nights only.
        Use the available functions to get today's date, look up previous travels,
        find flights, and find hotels.
        When you have gathered all the necessary information to make a booking,
        present the options to the user and ask for their confirmation.
        Use the following format to present options:
        For flights:
        Here is my recommendation for flight from <departure_city> (<departure_airport_code>) to <arrival_city> (<arrival_airport_code>:
        <airline> <flight_number> Departure: <departure_week_day> <departure_time> Arrival: <arrival_week_day> <arrival_time> Duration: <duration> Class: <class> Price: <price>
        For hotels:
        Here is my recommendation for hotel in <city>:
        <hotel_name> Check-In: <check_in_date> Check-Out: <check_out_date> Number of Guests: <number_of_guests> Price per Night: <price_per_night>
        For car rentals (if applicable):
        Here is my recommendation for car rental in <city>:
        <car_rental_company> Pick-Up: <pick_up_date> Drop-Off: <drop_off_date> Price per Day: <price_per_day>
        Remember to calculate the cost for car including fractional days as full days.
        Use markdown tables to present the options in a clear and organized manner.
        
        If user wants to look for alternative flights or hotels, you'll help them with that as well.

        If user wants to find some additional activities, you'll help them by listing max. 5 options
        but if they do not ask, you do not suggest any activities.
        You don't need to book the activities for them and you don't need to confirm them from user.
        User can book the activities separately if they want to.
        Do not include these in the total costs.

        Make sure you handle all the users requests in a single conversation.
        Include total costs at the end when you present the options to the user.
        Always ask for confirmation before finalizing the bookings to avoid any mistakes.
        Remind them that they need to book any possible activities separately
        as you will only finalize the flight, hotel, and car rental bookings.

        After you have received confirmation, then
        you can let user know that you're finalizing the travel arrangements
        and that they will get a confirmation email shortly including
        all the travel documents and calendar invites.
        Provide relevant travel greetings at the end after confirmation.",
    Arguments = new KernelArguments(new PromptExecutionSettings()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    })
};

Console.WriteLine("Type your message. Ctrl + C to exit");

var chatMessages = new List<ChatMessageContent>();
var initialMessage = @"I need to travel again to Stockholm next week Tuesday for 3 days.
Book me my usual hotel and rent a car as well.
Also, find something fun to do for Wednesday evening.";
var isInitialMessage = true;

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

static Func<int, string> GetPreviousTravelDetails()
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
            | Contoso Air   | CA123         | {departureTime:yyyy-MM-dd} 8:30 | {departureTime.AddHours(3):yyyy-MM-dd} 11:30 | 3h       | {travelClass} | $300    |
            | Fabrikam Air  | FA456         | {departureTime.AddHours(1):yyyy-MM-dd} 9:30 | {departureTime.AddHours(4):yyyy-MM-dd} 12:30 | 3h       | {travelClass} | $320    |

            Here are some return flight options from {arriveAirportCode} to {departureAirportCode}:

            | Airline       | Flight Number | Departure Time     | Arrival Time       | Duration | Class        | Price               |---------------|---------------|--------------------|--------------------|----------|--------------|---------|
            |---------------|---------------|--------------------|--------------------|----------|--------------|---------|
            | Contoso Air   | CA789         | {returnTime:yyyy-MM-dd} 18:00 | {returnTime.AddHours(3):yyyy-MM-dd} 21:00 | 3h       | {travelClass} | $300    |  
            | Fabrikam Air  | FA101         | {returnTime.AddHours(1):yyyy-MM-dd} 17:30 | {returnTime.AddHours(4):yyyy-MM-dd} 20:30 | 3h       | {travelClass} | $320    |
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

static Func<string, DateTime, DateTime, string> GetCarRentals()
{
    return (string city, DateTime checkInDate, DateTime checkOutDate) =>
    {
        Console.WriteLine($"<Function-GetCarRentals: Fetching car rentals in {city}>");
        Console.WriteLine($"<Function-GetCarRentals: Check-In Date {checkInDate}, Check-Out Date {checkOutDate}>");
        return $@"
            Here are some car rental options in {city}:
            | Car Rental Company | Pick-Up Date       | Drop-Off Date      | Price per Day |
            |---------------------|--------------------|--------------------|----------------|
            | Contoso Rentals     | {checkInDate:yyyy-MM-dd} | {checkOutDate:yyyy-MM-dd} | $50            |
            | Fabrikam Rentals    | {checkInDate:yyyy-MM-dd} | {checkOutDate:yyyy-MM-dd} | $60            |
            ";
    };
}

static Func<string, DateTime, string> GetActivities()
{
    return (string city, DateTime date) =>
    {
        Console.WriteLine($"<Function-GetActivities: Fetching activities in {city}>");
        Console.WriteLine($"<Function-GetActivities: Date {date}>");
        return $@"
            Here are some activity options in {city}:
            | Activity Name      | Date               | Duration           | Price           |
            |--------------------|--------------------|--------------------|------------------|
            | City Tour          | {date:yyyy-MM-dd} 17:00 | 3 hours           | $100            |
            | Museum Visit       | {date.AddDays(1):yyyy-MM-dd} 18:00 | 2 hours           | $50             |
            ";
    };
}
