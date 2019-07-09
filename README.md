# recursive-twitter
1. create an app through https://developer.twitter.com and set it so that "Access Permission" is set to "Read, write and Direct Messages"
2. edit `appsettings.json` to specify your Twitter app keys
3. on first startup, authenticate through browser to let your app post through you

the program will keep making tweets until it creates a reference loop by correctly guessing one of the two tweets' ID before posting it or until it hits rate limit (currently 300 tweets every 3 hours). it will then print statistics regarding guessed components and success probability

tweets are identified by their snowflake id, made of four components that vary constantly. if you can guess them, you can predict a tweet's URL before posting it. my code does this to create cyclical reference loops or 'recursive tweets'

the four components are sequence, datacenter id, worker id and timestamp. at first, i tried to repeat the first three components and guess the new timestamp through the delay between two tweets, as seen [here](https://github.com/pomber/escher-bot/). my success rate was 0.015% per attempt

then i noticed that some worker ids appeared more often than others. also i started using a trimmed mean of the delays between attempts. this approach increased my success rate to 0.1% per attempt. each session (150 attempts) had a 14% chance of success

making two tweets reply to each other is harder than guessing a tweet's own id because of the extra round trip of posting a second tweet, increasing the snowflake components variance. here were my average guess rates:
- sequence: 50%
- datacenter id: 97%
- worker id: 10%
- timestamp: 2%
