# recursive-twitter
1. create an app through https://developer.twitter.com and set it so that "Access Permission" is set to "Read, write and Direct Messages"
2. edit `appsettings.json` to specify your Twitter app keys
3. on first startup, authenticate through browser to let your app post through you

the program will keep making tweets until it creates a reference loop by correctly guessing one of the two tweets' ID before posting it or until it hits rate limit (currently 300 tweets every 3 hours). it will then print statistics regarding guessed components and success probability
