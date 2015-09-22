using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Simple.Logger.Tests
{
    public class LoggerTest
    {
        public LoggerTest()
        {
            Logger.RegisterWriter(d =>
            {
                Console.WriteLine(d);
            });
        }

        [Fact]
        public void LogMessage()
        {
            int k = 42;
            int l = 100;

            Logger.Trace().Message("Sample trace message, k={0}, l={1}", k, l).Write();
            Logger.Debug().Message("Sample debug message, k={0}, l={1}", k, l).Write();
            Logger.Info().Message("Sample informational message, k={0}, l={1}", k, l).Write();
            Logger.Warn().Message("Sample warning message, k={0}, l={1}", k, l).Write();
            Logger.Error().Message("Sample error message, k={0}, l={1}", k, l).Write();
            Logger.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
            Logger.Log(LogLevel.Info).Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
            Logger.Log(() => LogLevel.Info).Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
        }

        [Fact]
        public void LogInfoProperty()
        {
            int k = 42;
            int l = 100;

            Logger.Info()
                .Message("Sample informational message, k={0}, l={1}", k, l)
                .Property("Test", "Tesing properties")
                .Write();
        }

        [Fact]
        public void LogError()
        {
            string path = "blah.txt";
            try
            {
                string text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Logger.Error()
                    .Message("Error reading file '{0}'.", path)
                    .Exception(ex)
                    .Property("Test", "ErrorWrite")
                    .Write();
            }
        }

        [Fact]
        public void LogThreadProperty()
        {
            ManualResetEvent w1 = new ManualResetEvent(false);
            ManualResetEvent w2 = new ManualResetEvent(false);

            LogData d1 = null;
            LogData d2 = null;

            int l = 100;
            Logger.GlobalProperties["L"] = l.ToString();

            var t1 = new Thread(o =>
            {
                int k = 41;

                using (var state = Logger.ThreadProperties.Set("K", k))
                {

                    var builder = Logger.Info();

                    builder
                        .Message("Sample informational message, k={0}, l={1}", k, l)
                        .Property("Test", "Testing properties");


                    d1 = builder.LogData;

                }

                w1.Set();
            });


            var t2 = new Thread(o =>
            {
                int k = 42;

                Logger.ThreadProperties.Set("K", k);


                var builder = Logger.Info();

                builder
                    .Message("Sample informational message, k={0}, l={1}", k, l)
                    .Property("Test", "Testing properties");


                d2 = builder.LogData;

                Logger.ThreadProperties.Remove("K");

                w2.Set();
            });


            t1.Start();
            t2.Start();

            w1.WaitOne();
            w2.WaitOne();


            d1.Should().NotBeNull();

            var k1 = d1.Properties["K"];
            k1.Should().Be("41");

            d2.Should().NotBeNull();

            var k2 = d2.Properties["K"];
            k2.Should().Be("42");

            var l1 = d1.Properties["L"];
            var l2 = d2.Properties["L"];

            l1.Should().Be(l2);
        }

        [Fact]
        public void LoggerFromClass()
        {
            var logger = Logger.CreateLogger<LoggerTest>();

            int k = 42;
            int l = 100;

            logger.Info()
                .Message("Sample informational message from class, k={0}, l={1}", k, l)
                .Property("Test", "Tesing properties")
                .Write();

        }

        [Fact]
        public void LoggerWithDefaultProperty()
        {
            var logger = Logger.CreateLogger(c => c.Logger<LoggerTest>().Property("Default", "All Logs Have This"));

            int k = 42;
            int l = 100;

            logger.Info()
                .Message("Sample informational message with default property, k={0}, l={1}", k, l)
                .Property("Test", "Tesing properties")
                .Write();

        }
    }
}
