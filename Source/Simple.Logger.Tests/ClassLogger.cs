using System;
using System.IO;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Simple.Logger.Tests
{
    public class ClassLogger
    {
        private static readonly ILogger _logger = Logger.CreateLogger<ClassLogger>();

        public ClassLogger(ITestOutputHelper output)
        {
            var writer = new DelegateLogWriter(d => output.WriteLine(d.ToString()));
            Logger.RegisterWriter(writer);
        }

        [Fact]
        public void LogMessage()
        {
            int k = 42;
            int l = 100;

            _logger.Trace().Message("Sample trace message, k={0}, l={1}", k, l).Write();
            _logger.Debug().Message("Sample debug message, k={0}, l={1}", k, l).Write();
            _logger.Info().Message("Sample informational message, k={0}, l={1}", k, l).Write();
            _logger.Warn().Message("Sample warning message, k={0}, l={1}", k, l).Write();
            _logger.Error().Message("Sample error message, k={0}, l={1}", k, l).Write();
            _logger.Fatal().Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
            _logger.Log(LogLevel.Info).Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
            _logger.Log(() => LogLevel.Info).Message("Sample fatal error message, k={0}, l={1}", k, l).Write();
        }

        [Fact]
        public void LogInfoProperty()
        {
            int k = 42;
            int l = 100;

            _logger.Info()
                .Message("Sample informational message, k={0}, l={1}", k, l)
                .Property("Test", "Tesing properties")
                .Write();
        }

        [Fact]
        public void LogInterpolation()
        {
            int k = 42;
            int l = 100;

            // delay string interpolation formating till actual write of message
            _logger.Info()
                .Message(() => $"Sample informational message, k={k}, l={l}")
                .Property("Test", "Tesing properties")
                .Write();
        }
        [Fact]
        public void LogExtension()
        {
            int k = 42;
            int l = 100;

            // delay string interpolation formating till actual write of message
            _logger.Info(() => $"Sample informational message, k={k}, l={l}");
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
                _logger.Error()
                    .Message("Error reading file '{0}'.", path)
                    .Exception(ex)
                    .Property("Test", "ErrorWrite")
                    .Write();
            }
        }

        [Fact]
        public void CorrectLoggerName()
        {
            int k = 42;
            int l = 100;

            var builder = _logger.Info()
                .Message("Sample informational message, k={0}, l={1}", k, l)
                .Property("Test", "Tesing properties");


            builder.LogData.Logger.Should().Be("Simple.Logger.Tests.ClassLogger");
        }
    }
}