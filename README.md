# Smart-X Part 2: Real-Time Processing and Device Management

**Smart-X IoT Mesh Ecosystem \| Portfolio of Evidence (PoE) --- Part 2**

Smart-X is an educational Internet of Things (IoT) gateway and
device-management application. Part 2 extends the gateway with telemetry
processing, device management, command execution, historical readings,
alert handling and rule-based recommendations.

The application uses ASP.NET Core Minimal APIs for HTTP endpoints and a
browser-based dashboard for monitoring and managing simulated IoT
devices (Microsoft, n.d.-a).

## Contents

-   [Features](#features)
-   [Technology stack](#technology-stack)
-   [Requirements](#requirements)
-   [Installation and setup](#installation-and-setup)
-   [Running the application](#running-the-application)
-   [Demonstration guide](#demonstration-guide)
-   [Data structures and assessment
    mapping](#data-structures-and-assessment-mapping)
-   [API reference](#api-reference)
-   [Example requests](#example-requests)
-   [Testing](#testing)
-   [Limitations and future
    improvements](#limitations-and-future-improvements)
-   [References](#references)

## Features

### Telemetry processing

-   Accepts sensor readings through HTTP endpoints.
-   Records telemetry for device monitoring and historical review.
-   Supports simulated or seeded telemetry for demonstrations.
-   Maintains timestamp-ordered history for reviewing device activity.

### Device management

-   Maintains a device registry.
-   Provides device summaries and filtering by category or alert status.
-   Supports device lookup by identifier.

### Command stream and undo

-   Supports simulated commands such as `pump-on`, `pump-off`,
    `relay-on`, `relay-off`, `valves-open`, `valves-close` and `stop`.
-   Records command history.
-   Supports undoing the most recent supported command.

### Alerts and recommendations

-   Evaluates telemetry against configured alert rules.
-   Tracks active alerts and prevents duplicate active-alert entries.
-   Provides recommendations based on observed telemetry and repeated
    command patterns.
-   Displays operational metrics for devices, alerts, history and undo
    operations.

### Dashboard and updates

-   Provides a main navigation page and a Part 2 command dashboard.
-   Uses the configured telemetry hub for server-to-client updates where
    supported.
-   Refreshes dashboard information through client-side requests.
    SignalR is designed to support real-time updates from server-side
    code to connected clients (Microsoft, n.d.-b).

## Technology stack

  -----------------------------------------------------------------------
  Technology                          Purpose
  ----------------------------------- -----------------------------------
  C#                                  Application and service logic

  .NET 10 / ASP.NET Core              Application runtime and web
                                      framework

  ASP.NET Core Minimal APIs           HTTP API endpoints (Microsoft,
                                      n.d.-a)

  Razor Pages, HTML, CSS and          Browser-based interface
  JavaScript                          

  SignalR                             Real-time updates where configured
                                      (Microsoft, n.d.-b)

  .NET generic collections            Queues, stacks, dictionaries and
                                      sets (Microsoft, n.d.-c)

  xUnit                               Automated unit testing
  -----------------------------------------------------------------------

## Requirements

Install the following software:

-   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
-   Visual Studio or Visual Studio Code
-   A modern web browser

Check that the SDK is installed:

``` bash
dotnet --version
```

The installed SDK must support the project's target framework.

## Installation and setup

### 1. Clone or extract the repository

If the project is hosted on GitHub, clone your repository and enter its
directory:

``` bash
git clone https://github.com/ST10462019/prog7312-2026-poe-part2-st10462019.git
cd YOUR-REPOSITORY
```

### 2. Restore dependencies

``` bash
dotnet restore .\SmartX.sln
```

### 3. Build the solution

``` bash
dotnet build .\SmartX.sln
```

Resolve any build errors before proceeding.

## Running the application

From the folder containing `SmartX.sln`, start the gateway:

``` bash
dotnet run --project .\SmartX.Gateway\SmartX.Gateway.csproj
```

Keep the terminal open and open the local URL printed by the
application. The port may differ depending on the launch profile.

  Route          Purpose
  -------------- ------------------------------------------------------
  `/`            Main navigation page
  `/Dashboard`   Sensor dashboard, if configured
  `/Commands`    Part 2 command and device-management dashboard
  `/swagger`     Interactive API documentation, if Swagger is enabled

If a route is unavailable, check the routes configured in the source
code. Stop the application with `Ctrl+C`.

## Demonstration guide

Use this sequence for a demonstration video or assessment walkthrough:

1.  **Home page:** Show the navigation menu and explain that network
    topology is reserved for Part 3.
2.  **Device registration:** Register a simulated device using the
    existing sensor-registration workflow. Record the exact device
    identifier returned by the application.
3.  **Telemetry ingestion:** Submit a telemetry reading or use the
    telemetry seeder. Show the resulting reading and historical record.
4.  **Critical alert:** Submit a reading that meets a configured alert
    rule, such as low environmental moisture. Show the alert and any
    recommendation.
5.  **Manual command:** Execute a supported command for the registered
    device, such as `pump-on`, and show the command result.
6.  **Undo:** Use the undo operation and show the resulting simulated
    state.
7.  **Filtering and history:** Filter the device list by category or
    alert status and inspect telemetry history.
8.  **Metrics:** Show the available queue, device, alert, history and
    undo counters.
9.  **API documentation:** If enabled, open `/swagger` and demonstrate
    the available endpoints.
10. **Automated tests:** Run `dotnet test .\SmartX.sln` and show the
    results.

Only demonstrate features that are implemented and working in the
submitted version. Use the same registered device identifier in
telemetry and command requests.

## Data structures and assessment mapping

.NET provides collection types suited to different access and processing
patterns (Microsoft, n.d.-c).

  --------------------------------------------------------------------------------------
  Requirement             Implementation                         Purpose
  ----------------------- -------------------------------------- -----------------------
  FIFO telemetry queue    `Queue<T>` in the device-management    Processes normal
                          service                                telemetry in first-in,
                                                                 first-out order

  Critical-event queue    `PriorityQueue<TElement, TPriority>`   Stores alerts with
                                                                 associated priorities.
                                                                 The lowest priority
                                                                 value is dequeued first
                                                                 (Microsoft, n.d.-d)

  Command undo            `Stack<T>`                             Supports last-in,
                                                                 first-out undo
                                                                 behaviour

  Device registry         `Dictionary<TKey, TValue>`             Looks up device state
                                                                 by identifier

  Timestamp-sorted        `SortedDictionary<TKey, TValue>`       Stores records in
  history                                                        sorted key order

  Duplicate-alert         `HashSet<T>`                           Tracks unique
  tracking                                                       active-alert keys

  Search and filtering    Device API and dashboard controls      Narrows devices by
                                                                 category or alert
                                                                 status

  Manual overrides        Command execution and undo endpoints   Changes simulated
                                                                 device state

  Recommendations         Rule-based recommendation service      Suggests actions based
                                                                 on configured alerts
                                                                 and repeated command
                                                                 patterns

  Dashboard updates       Razor Pages, JavaScript polling and    Supports dashboard
                          SignalR where configured               navigation and updates
  --------------------------------------------------------------------------------------

### Recommendation logic

The recommendation engine uses configured rules and observed application
activity. For example, a low-moisture alert can prompt an irrigation
recommendation, while repeated pump activations can prompt the user to
inspect the sensor, control logic or system for a possible issue.

These are heuristic recommendations, not predictions from a trained
machine-learning model. Their usefulness depends on the telemetry and
command history available during the session.

## API reference

The following routes reflect the Part 2 API design. Confirm exact
request and response schemas against the source code or Swagger output
before integrating another application.

  --------------------------------------------------------------------------------------------
  Method                  Route                                        Purpose
  ----------------------- -------------------------------------------- -----------------------
  `POST`                  `/api/telemetry`                             Submits a telemetry
                                                                       reading

  `GET`                   `/api/commands/devices?category=&alert=`     Lists devices, with
                                                                       optional category and
                                                                       alert filters

  `GET`                   `/api/commands/alerts?take=100`              Retrieves active alerts

  `GET`                   `/api/commands/history?take=200&deviceId=`   Retrieves telemetry
                                                                       history, optionally for
                                                                       one device

  `GET`                   `/api/commands/history/commands?take=100`    Retrieves command
                                                                       history

  `GET`                   `/api/commands/suggestions`                  Retrieves
                                                                       recommendations

  `GET`                   `/api/commands/metrics`                      Retrieves service
                                                                       metrics

  `POST`                  `/api/commands/execute`                      Executes a supported
                                                                       simulated command

  `POST`                  `/api/commands/undo`                         Reverts the most recent
                                                                       supported command

  Sensor registration     Configured in `Program.cs`                   Registers a device
  route                                                                through the existing
                                                                       ingestion workflow
  --------------------------------------------------------------------------------------------

## Example requests

### Submit telemetry

This example submits a low environmental-moisture reading. Replace the
URL with the local address printed when the application starts. The
request fields must match the API model used by the current build.

``` bash
curl -X POST "http://localhost:5000/api/telemetry" \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "ESP32-01",
    "location": "Zone 1",
    "category": "Environmental",
    "dataType": "float",
    "value": 12.0,
    "unit": "%",
    "timestamp": "2026-10-18T18:00:00Z",
    "sequenceNumber": 9001
  }'
```

### Execute a simulated command

Use a device identifier that has already been registered.

``` bash
curl -X POST "http://localhost:5000/api/commands/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "ESP32-01",
    "command": "pump-on",
    "reason": "Low moisture simulation"
  }'
```

Supported command names are defined by the command service. If your API
model uses different property names, adjust the JSON to match the
implementation.

## Testing

Run all tests from the directory containing `SmartX.sln`:

``` bash
dotnet test .\SmartX.sln
```

To build and test in sequence:

``` bash
dotnet build .\SmartX.sln
dotnet test .\SmartX.sln
```

The Part 2 test project is intended to cover key behaviours such as
command execution and undo, unknown-device handling, alert deduplication
and history ordering. Confirm the results locally before submitting the
project.

## Limitations and future improvements

-   **In-memory state:** Device state, alerts, command history and
    telemetry history may reset when the application restarts.
-   **Simulated control:** Commands do not control physical hardware
    unless a device integration is implemented.
-   **Rule-based recommendations:** Suggestions use explicit heuristics
    rather than a trained predictive model.
-   **Security:** A production deployment would require suitable
    authentication, authorisation, secure transport, input validation
    and audit controls.
-   **Persistence and scalability:** A future version could add a
    database, durable message processing and telemetry-retention
    policies.
-   **Part 3:** Network topology visualisation and mesh-routing
    functionality can be added as the next stage.

## References

Microsoft (n.d.-a) *Minimal APIs quick reference for ASP.NET Core*.
Microsoft Learn. Available at:
https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0
(Accessed: 15 October 2026).

Microsoft (n.d.-b) *Overview of ASP.NET Core SignalR*. Microsoft Learn.
Available at: https://learn.microsoft.com/en-us/aspnet/core/signalr
(Accessed: 16 October 2026).

Microsoft (n.d.-c) *Commonly used collection types*. Microsoft Learn.
Available at:
https://learn.microsoft.com/en-us/dotnet/standard/collections/commonly-used-collection-types
(Accessed: 18 October 2026).

Microsoft (n.d.-d) *PriorityQueue\<TElement,TPriority\> Class*.
Microsoft Learn. Available at:
https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2
(Accessed: 18 October 2026).

Microsoft (n.d.-e) *.NET downloads*. Microsoft. Available at:
https://dotnet.microsoft.com/download (Accessed: 19 October 2026).

------------------------------------------------------------------------

**Smart-X IoT Mesh Ecosystem --- PoE Part 2: Real-Time Processing and Device
Management**

