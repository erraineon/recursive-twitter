using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqToTwitter;
using Newtonsoft.Json;

namespace RecursiveTwitter
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var authorizer = await Authorize();
            using (var twitter = new TwitterContext(authorizer))
            {
                await RunBot(twitter);
            }

            Console.WriteLine("all set");
            Console.ReadLine();
        }

        private static async Task RunBot(TwitterContext twitter)
        {
            //pseudorandom to avoid having duplicate initial tweets on different iterations
            ulong lastTimestamp = (ulong) DateTime.Now.Ticks; 
            uint sequence = default,
                lastDatacenterId = default,
                lastWorkerId = default,
                lastDelay = default,
                guessedWorkerId = default,
                guessedDelay = default;
            int sequenceHits = default,
                lastDatacenterIdHits = default,
                lastWorkerIdHits = default,
                lastDelayHits = default,
                guessedWorkerIdHits = default,
                guessedDelayHits = default,
                attempts = default;

            var delays = new List<uint>();
            var workerIdCounts = new Dictionary<uint, int>();

            void IncreaseWorkerIdCount(uint workerId)
            {
                workerIdCounts[workerId] =
                    workerIdCounts.TryGetValue(workerId, out var hits) ? hits + 1 : 1;
            }

            var myName = twitter.Authorizer.CredentialStore.ScreenName;

            try
            {
                while (true)
                {
                    var expected = new Snowflake(sequence, lastDatacenterId, guessedWorkerId,
                        lastTimestamp + (guessedDelay == default ? lastDelay : guessedDelay));
                    var first = await twitter.TweetAsync($"a https://twitter.com/{myName}/status/{expected.Id}");
                    // get any useful information from this
                    IncreaseWorkerIdCount(new Snowflake(first.StatusID).WorkerId);
                    var second = await twitter.TweetAsync($"b https://twitter.com/{myName}/status/{first.StatusID}");
                    var actual = new Snowflake(second.StatusID);

                    // compare state to actual
                    var sequenceMatch = expected.Sequence == actual.Sequence;
                    var datacenterIdMatch = expected.DatacenterId == actual.DatacenterId;
                    var workerIdMatch = expected.WorkerId == actual.WorkerId;
                    var timestampMatch = expected.Timestamp == actual.Timestamp;
                    // collect this info for stats, regardless of used strategy
                    var lastWorkerIdMatch = lastWorkerId == actual.WorkerId;
                    var lastTimestampMatch = lastTimestamp + lastDelay == actual.Timestamp;
                    var guessedWorkerIdMatch = guessedWorkerId == actual.WorkerId;
                    var guessedTimestampMatch = lastTimestamp + guessedDelay == actual.Timestamp;

                    // overwrite state
                    lastWorkerId = actual.WorkerId;
                    lastDatacenterId = actual.DatacenterId;
                    lastDelay = (uint) (actual.Timestamp - lastTimestamp);
                    lastTimestamp = actual.Timestamp;
                    IncreaseWorkerIdCount(actual.WorkerId);
                    // use the most frequent worker id as the guess
                    guessedWorkerId = workerIdCounts.OrderByDescending(pair => pair.Value).First().Key;
                    // on the first iteration the delay will have skewed data, so don't add it
                    if (attempts > 0) delays.Add(lastDelay);
                    // use a trimmed median to find likely delay values
                    const double trimPercentile = .1;
                    var delaysToTrim = (int) (delays.Count * trimPercentile);
                    if (delays.Count > delaysToTrim * 2)
                        guessedDelay = (uint) delays
                            .Skip(delaysToTrim)
                            .Take(delays.Count - delaysToTrim)
                            .Average(d => (double) d);

                    // show results
                    var builder = new StringBuilder();

                    void AppendBool(string label, bool value) => builder.Append($"{label}:{(value ? '█' : ' ')}");

                    AppendBool("S", sequenceMatch);
                    AppendBool("D", datacenterIdMatch);
                    AppendBool("W", workerIdMatch);
                    AppendBool("T", timestampMatch);
                    builder.Append(" --- ");
                    AppendBool("WL", lastWorkerIdMatch);
                    AppendBool("WG", guessedWorkerIdMatch);
                    AppendBool("TL", lastTimestampMatch);
                    AppendBool("TG", guessedTimestampMatch);
                    builder.Append($"seq: {actual.Sequence} dac: {lastDatacenterId} lwrk: {lastWorkerId} " +
                                   $"gwrk: {guessedWorkerId} ldel: {lastDelay} gdel: {guessedDelay};");
                    Console.WriteLine(builder.ToString());

                    // gather stats
                    if (sequenceMatch) sequenceHits++;
                    if (lastWorkerIdMatch) lastWorkerIdHits++;
                    if (datacenterIdMatch) lastDatacenterIdHits++;
                    if (lastTimestampMatch) lastDelayHits++;
                    if (guessedTimestampMatch) guessedDelayHits++;
                    if (guessedWorkerIdMatch) guessedWorkerIdHits++;
                    attempts++;

                    // if success, stop
                    if (sequenceMatch && workerIdMatch && datacenterIdMatch && timestampMatch)
                    {
                        Console.WriteLine("thats the tea sis");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                // usually rate limit excess
                Console.WriteLine(e);
            }
            finally
            {
                // print gathered stats
                var total = attempts;
                var sp = (float) sequenceHits / total;
                var wlp = (float) lastWorkerIdHits / total;
                var dp = (float) lastDatacenterIdHits / total;
                var tlp = (float) lastDelayHits / total;
                var tap = (float) guessedDelayHits / total;
                var wap = (float) guessedWorkerIdHits / total;
                Console.WriteLine($"S: {sequenceHits}/{total} p:{sp:P}");
                Console.WriteLine($"W: {lastWorkerIdHits}/{total} p:{wlp:P}");
                Console.WriteLine($"D: {lastDatacenterIdHits}/{total} p:{dp:P}");
                Console.WriteLine($"T: {lastDelayHits}/{total} p:{tlp:P}");
                Console.WriteLine($"TA: {guessedDelayHits}/{total} p:{tap:P}");
                Console.WriteLine($"WA: {guessedWorkerIdHits}/{total} p:{wap:P}");
                Console.WriteLine($"Chance of success: {sp * wap * dp * tap:P}");
            }
        }

        private static async Task<PinAuthorizer> Authorize()
        {
            const string settingsPath = "appsettings.json";
            var twitterOptions =
                JsonConvert.DeserializeObject<TwitterOptions>(await File.ReadAllTextAsync(settingsPath));
            var auth = new PinAuthorizer
            {
                CredentialStore = new InMemoryCredentialStore
                {
                    ConsumerKey = twitterOptions.ConsumerKey,
                    ConsumerSecret = twitterOptions.ConsumerSecret
                },
                GoToTwitterAuthorization = pageLink => Process.Start("cmd", $"/c start {pageLink}"),
                GetPin = () =>
                {
                    Console.Write("enter your PIN: ");
                    return Console.ReadLine();
                }
            };
            var credentials = auth.CredentialStore;
            if (string.IsNullOrEmpty(twitterOptions.OAuthToken))
            {
                await auth.AuthorizeAsync();
                twitterOptions.OAuthToken = credentials.OAuthToken;
                twitterOptions.OAuthTokenSecret = credentials.OAuthTokenSecret;
                twitterOptions.ScreenName = credentials.ScreenName;
                await File.WriteAllTextAsync(settingsPath,
                    JsonConvert.SerializeObject(twitterOptions, Formatting.Indented));
            }
            else
            {
                credentials.OAuthToken = twitterOptions.OAuthToken;
                credentials.OAuthTokenSecret = twitterOptions.OAuthTokenSecret;
                credentials.ScreenName = twitterOptions.ScreenName;
            }

            return auth;
        }
    }
}