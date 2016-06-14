# Getting started with Infinario C# SDK
[![Slack Status](http://community.exponea.com/badge.svg)](https://community.exponea.com/)

## Installation

1.  Download the latest **InfinarioSDK.dll**
2.  In your project right click on **References -> Add Reference... -> Browse ->** select downloaded **InfinarioSDK.dll**
3.  Add following packages through NuGet Package Manager. In your project right click on **References -> Manage NuGet Packages... -> Browse ->

    1.  **System.Data.SQLite** to make access to SQL database

## Usage

### Basic Tracking

To start tracking, you need to know your `projectToken`. To initialize the tracking, simply get an instance of the `Infinario` class and call `Initialize`:

    var infinario = Infinario.Infinario.GetInstance();
    infinario.Initialize("projectToken");

You can also specify app version:

    infinario.Initialize("projectToken", "1.5.0");

Now you can track events by calling the `Track` method:

    infinario.Track("my_user_action");

What happens now, is that an event called `my_user_action` is recorded for the current player.

### Identifying Players

To control the identity of the current player use the `Identify` method. By calling

    infinario.Identify("player@example.com");

you can register a new player in Infinario. All events you track by the `Track` method from now on will belong to this player. To switch to an existing player, simply call `Identify` with his name. You can switch the identity of the current player as many times as you need to.

### Anonymous Players

Up until you call `Identify` for the first time, all tracked events belong to an anonymous player (internally identified with a cookie). Once you call `Identify`, the previously anonymous player is automatically merged with the newly identified player.

### Anonymizing Players

You can forget the identity of your player (remove all the ids and generate new cookie) by calling `Unidentify`.

    infinario.Unidentify();

### Adding Properties

Both `Identify` and `Track` accept an optional dictionary parameter that can be used to add custom information (properties) to the respective entity. Usage is straightforward:

    infinario.Track("my_player_action", new Dictionary<string,object> {{"daily_score", 4700}});                                       

    infinario.Identify("player@example.com", new Dictionary<string,object> {
        {"first_name", "John"},
        {"last_name", "Doe"}
    }); 

    infinario.Update(new Dictionary<string,object> {{"level", 1}}); // A shorthand for adding properties to the current player

### Virtual payment

If you use virtual payments (e.g. purchase with in-game gold, coins, ...) in your project, you can track them with a call to TrackVirtualPayment.

    infinario.TrackVirtualPayment("gold", 3, "SWORD", "SWORD.TYPE");

### Player Sessions

Session is a real time spent in the game. Tracking of sessions produces two events, `session_start` and `session_end`. To track session start call `TrackSessionStart()` from where whole game gets focus and to track session end call `TrackSessionEnd()` from whole game loses focus.

    // track session start
    infinario.TrackSessionStart();
    // or with properties
    ifnfinario.TrackSessionStart(new Dictionary<string,object> {{"level", 1}});

    // track session end
    infinario.TrackSessionEnd();
    // or with properties
    ifnfinario.TrackSessionEnd(new Dictionary<string,object> {{"level", 1}});

Both events contain the timestamp of the occurence together with basic attributes about the device (OS, OS version, SDK, SDK version and device model). Event `session_end` contains also the duration of the session in seconds.

### Timestamps

The SDK automatically adds timestamps to all events. To specify your own timestamp, use one of the following method overload:

    infinario.Track("my_player_action", <properties> , <long_your_tsp>);	

### Disposing of Infinario instance

All tracked events are stored in the local SQL database. By default, Infinario SDK automatically takes care of flushing events to the Infinario API in separate thread that is started after calling `Initialize`. It is mandatory to call `Dispose` at the end of game since this will stop the thread and flush the events.

    // all the events will be sent to Infinario API right now
    infinario.Dispose();  

    // events won't be sent now but they will be persisted on local storage. Infinario will send them after next call to Initialize
    infinario.Dispose(false);

### Logging

By default Infinario does not log anything. You can turn on logging by specifying logger instance in `Initialize` call.

    infinario.Initialize("projectToken", "1.5.0", null, new Infinario.Logging.ConsoleLogger());

`ConsoleLogger` logs everything to console. You can implement your custom logger by implementing `Infinario.Logging.Logger` interface

### Thread safety and blocking

Infinario SDK implementation is thread safe. You can call `GetInstance` from any thread, just make sure that both `Initialize` and `Dispose` are called once. `Initialize` should be called before `Dispose`. It is possible to reinitialize already disposed Infinario object by calling `Initialize` once again. Every method on Infinario object except `Dispose` is non-blocking. We use separate threads for storing commands in SQLite database and for sending them to our API.

Thread safety is guaranteed only when you are not modifying parameters used in Infinario calls afterwards. E.g. following use case is invalid and should be avoided:

    Dictionary<string, object> properties = new Dictionary<string, object>();
    properties["item"] = "sword";
    infinario.Track("used_item", properties);

    // this is invalid call and should be avoided since it will cause race condition
    properties["item"] = "key";    

### Tracking prior to initialization

You can call any method on `Infinario` object except `Dispose` prior to `Initialize` call. All the commands are queued and will be executed after initialization. Methods tracked from another thread while calling `Dispose` are not garanteed to be executed.

### Customizing working directory for SQLite

By default Infinario stores datafile for SQLite database in current working directory. You can change this while calling `Initialize`.

    infinario.Initialize("projectToken", "1.5.0", null, new NullLoger(), "specifyWorkingDirectoryHere");

### Offline

If your application goes offline, the SDK guarantees you to re-send the events later unless the user is offline for more then one day of continous use. This synchronization is transparent to you and happens in the background.

