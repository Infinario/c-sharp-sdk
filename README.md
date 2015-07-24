<h1>Getting started with Infinario C# SDK</h1>

<h2>Installation</h2>
<ol>
	<li>Download the latest <b>InfinarioSDKC.dll</b></li>
	<li>In your project right click on <b>References -&gt; Add Reference... -&gt; Browse -&gt; </b> select downloaded <b>InfinarioSDKC.dll</b></li>
</ol>

<h2>Usage</h2>

<h3>Basic Tracking</h3>

To start tracking, you need to know your <code>company_token</code>. To initialize the tracking, simply create an instance of the <code>Infinario</code> class:

<pre><code>var infinario = new Infinario.Infinario("your_company_token");

//track your app version code
var infinario = new Infinario.Infinario("your_company_token", "1.5.0");</code></pre>

<br>
<p>Now you can track events by calling the <code>Track</code> method:</p>
<pre><code>infinario.Track("my_user_action");</code></pre>
What happens now, is that an event called ```my_user_action``` is recorded for the current player.

### Identifying Players
To control the identity of the current player use the ```Identify``` method. By calling
<pre><code>infinario.Identify("player@example.com");</code></pre>

you can register a new player in Infinario. All events you track by the ```Track``` method from now on will belong to this player. To switch to an existing player, simply call ```Identify``` with his name. You can switch the identity of the current player as many times as you need to.

### Anonymous Players
Up until you call ```Identify``` for the first time, all tracked events belong to an anonymous player (internally identified with a cookie). Once you call ```Identify```, the previously anonymous player is automatically merged with the newly identified player.

### Adding Properties
Both ```Identify``` and ```Track``` accept an optional dictionary parameter that can be used to add custom information (properties) to the respective entity. Usage is straightforward:

<pre><code>infinario.Track("my_player_action", new Dictionary&lt;string,object&gt; {{"daily_score", 4700}});                                       

infinario.Identify("player@example.com", new Dictionary&lt;string,object&gt; {
                                                          {"first_name", "John"},
                                                          { "last_name", "Doe" }
                                                        }); 
                                                        
infinario.Update(new Dictionary&lt;string,object&gt; {{"level", 1}}); // A shorthand for adding properties to the current customer
</code></pre>

<h3>Virtual payment</h3>
If you use virtual payments (e.g. purchase with in-game gold, coins, ...) in your project, you can track them with a call to TrackVirtualPayment.
<pre><code>infinario.TrackVirtualPayment("gold", 3, "SWORD", "SWORD.TYPE");</pre></code>

<h3>Player Sessions</h3>
Infinario automatically manages player sessions. Each session starts with a <code>session_start</code> event and ends with <code>session_end</code>.

Once started, the SDK tries to recreate the previous session from its persistent cache. If it fails to, or the session has already expired it automatically creates a new one.



<h3>Timestamps</h3>
The SDK automatically adds timestamps to all events. To specify your own timestamp, use one of the following method overloads:
<pre><code>infinario.Track("my_player_action", &lt;long_your_tsp&gt;);
infinario.Track("my_player_action", &lt;properties&gt; , &lt;long_your_tsp&gt;);	
</code></pre>

<h3>Offline Behavior</h3>

Once instantized, the SDK collects and sends all tracked events continuously to the Infinario servers. 

However, if your application goes offline, the SDK guarantees you to re-send the events once online again (up to a approximately 5k offline events). This synchronization is transparent to you and happens in the background.

<h3>Final Remarks</h3>
- Make sure you create at most one instance of ```Infinario``` during your application lifetime.
- If you wish to override some of the capabilities (e.g. session management), please note that we will not be able to give you any guarantees.