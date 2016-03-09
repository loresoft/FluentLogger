using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using NLog.Fluent;

namespace Fluent.Logger.Performance
{
    public class Program
    {
        private static readonly ILogger _simple = Fluent.Logger.Logger.CreateLogger<LoggingTest>();

        static void Main(string[] args)
        {
            var writer = new DelegateLogWriter(d => { });
            Logger.RegisterWriter(writer);


            Console.WriteLine("Press any key to begin");
            Console.ReadKey();

            //Loop();
            ParallelLoop();

            //var summary = BenchmarkRunner.Run<LoggingTest>();


            Console.WriteLine("Press any key to end");
            Console.ReadKey();
        }


        static void Loop()
        {
            int k = 42;
            for (int i = 0; i < 1000000; i++)
            {
                _simple.Trace().Message("Sample trace message, k={0}, l={1}", k, i).Write();
                _simple.Debug().Message("Sample debug message, k={0}, l={1}", k, i).Write();
                _simple.Info().Message(() => $"Sample informational message, k={k}, l={i}").Write();
                _simple.Warn().Message("Sample warning message, k={0}, l={1}", k, i).Write();
                _simple.Error().Message("Sample error message, k={0}, l={1}", k, i).Write();
                _simple.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, i).Write();


                _simple
                    .Debug()
                    .Property("Test", "value")
                    .Property("Time", DateTime.Now)
                    .Message("Blah {0}", "format")
                    .Write();


                _simple
                    .Debug()
                    .Property("Test", "value")
                    .Property("Time", DateTime.Now)
                    .Message(() => $"Sample debug message, k={k}, l={i}")
                    .Write();

            }
        }

        static void ParallelLoop()
        {
            int k = 42;

            // Create a high demand for objects.
            Parallel.For(0, 1000000, (i, loopState) =>
            {
                _simple.Trace().Message("Sample trace message, k={0}, l={1}", k, i).Write();
                _simple.Debug().Message("Sample debug message, k={0}, l={1}", k, i).Write();
                _simple.Info().Message(() => $"Sample informational message, k={k}, l={i}").Write();
                _simple.Warn().Message("Sample warning message, k={0}, l={1}", k, i).Write();
                _simple.Error().Message("Sample error message, k={0}, l={1}", k, i).Write();
                _simple.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, i).Write();


                _simple
                    .Debug()
                    .Property("Test", "value")
                    .Property("Time", DateTime.Now)
                    .Message("Blah {0}", "format")
                    .Write();


                _simple
                    .Debug()
                    .Property("Test", "value")
                    .Property("Time", DateTime.Now)
                    .Message(() => $"Sample debug message, k={k}, l={i}")
                    .Write();

            });
        }
    }



    [Config(typeof(Config))]
    public class LoggingTest
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                Add(new Job { TargetCount = 100  });
            }
        }


        private static readonly ILogger _simple = Fluent.Logger.Logger.CreateLogger<LoggingTest>();
        private static readonly NLog.ILogger _nlog = NLog.LogManager.GetCurrentClassLogger();

        [Benchmark]
        public void SimpleLogger()
        {
            int k = 42;
            int l = 100;

            _simple.Trace().Message("Sample trace message, k={0}, l={1}", k, l).Write();
            _simple.Debug().Message("Sample debug message, k={0}, l={1}", k, l).Write();
            _simple.Info().Message("Sample informational message, k={0}, l={1}", k, l).Write();
            _simple.Warn().Message("Sample warning message, k={0}, l={1}", k, l).Write();
            _simple.Error().Message("Sample error message, k={0}, l={1}", k, l).Write();
            _simple.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, l).Write();

            _simple
                .Debug()
                .Property("Test", "value")
                .Property("Time", DateTime.Now)
                .Message("Blah {0}", "format")
                .Write();
        }

        [Benchmark]
        public void NLogLogger()
        {
            int k = 42;
            int l = 100;

            _nlog.Trace().Message("Sample trace message, k={0}, l={1}", k, l).Write();
            _nlog.Debug().Message("Sample debug message, k={0}, l={1}", k, l).Write();
            _nlog.Info().Message("Sample informational message, k={0}, l={1}", k, l).Write();
            _nlog.Warn().Message("Sample warning message, k={0}, l={1}", k, l).Write();
            _nlog.Error().Message("Sample error message, k={0}, l={1}", k, l).Write();
            _nlog.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, l).Write();

            _nlog
                .Debug()
                .Property("Test", "value")
                .Property("Time", DateTime.Now)
                .Message("Blah {0}", "format")
                .Write();
        }


    }

}
