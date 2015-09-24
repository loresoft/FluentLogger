using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;

namespace Simple.Logger
{
    /// <summary>
    /// Defines available log levels.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Trace log level.</summary>
        Trace = 0,
        /// <summary>Debug log level.</summary>
        Debug = 1,
        /// <summary>Info log level.</summary>
        Info = 2,
        /// <summary>Warn log level.</summary>
        Warn = 3,
        /// <summary>Error log level.</summary>
        Error = 4,
        /// <summary>Fatal log level.</summary>
        Fatal = 5,
    }

    /// <summary>
    /// A logger <see langword="interface"/> for starting log messages.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Gets the logger name.
        /// </summary>
        /// <value>
        /// The logger name.
        /// </value>
        string Name { get; }

        /// <summary>
        /// Gets the logger initial default properties.  All values are copied to each log.
        /// </summary>
        /// <value>
        /// The logger initial default properties.
        /// </value>
        IPropertyContext Properties { get; }


        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the specified <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder Log(LogLevel logLevel);

        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the computed <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevelFactory">The log level factory.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder Log(Func<LogLevel> logLevelFactory);

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Trace"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Trace();

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Debug"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Debug();

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Info"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Info();

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Warn"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Warn();

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Error"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Error();

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Fatal"/> logger.
        /// </summary>
        /// <returns>A fluent Logger instance.</returns>
        ILogBuilder Fatal();
    }

    /// <summary>
    /// A logger class for starting log messages.
    /// </summary>
    public sealed class Logger : ILogger
    {
        private static readonly object _writerLock;
        private static Action<LogData> _logAction;
        private static ILogWriter _logWriter;
        private static bool _hasSearched;

        // only create if used
        private static readonly Lazy<IPropertyContext> _asyncProperties;
        private static readonly ThreadLocal<IPropertyContext> _threadProperties;
        private static readonly Lazy<IPropertyContext> _globalProperties;

        private readonly Lazy<IPropertyContext> _properties;


        /// <summary>
        /// Initializes the <see cref="Logger"/> class.
        /// </summary>
        static Logger()
        {
            _writerLock = new object();
            _logAction = DebugWrite;
            _hasSearched = false;

            _globalProperties = new Lazy<IPropertyContext>(CreateGlobal);
            _threadProperties = new ThreadLocal<IPropertyContext>(CreateLocal);
            _asyncProperties = new Lazy<IPropertyContext>(CreateAsync);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        public Logger()
        {
            _properties = new Lazy<IPropertyContext>(() => new PropertyContext());
        }


        /// <summary>
        /// Gets the global property context.  All values are copied to each log on write.
        /// </summary>
        /// <value>
        /// The global property context.
        /// </value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static IPropertyContext GlobalProperties
        {
            get { return _globalProperties.Value; }
        }

        /// <summary>
        /// Gets the thread-local property context.  All values are copied to each log on write.
        /// </summary>
        /// <value>
        /// The thread-local property context.
        /// </value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static IPropertyContext ThreadProperties
        {
            get { return _threadProperties.Value; }
        }

        /// <summary>
        /// Gets the property context that maintains state across asynchronous tasks and call contexts. All values are copied to each log on write.
        /// </summary>
        /// <value>
        /// The asynchronous property context.
        /// </value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static IPropertyContext AsyncProperties
        {
            get { return _asyncProperties.Value; }
        }


        /// <summary>
        /// Gets the logger initial default properties.  All values are copied to each log.
        /// </summary>
        /// <value>
        /// The logger initial default properties.
        /// </value>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IPropertyContext Properties
        {
            get { return _properties.Value; }
        }

        /// <summary>
        /// Gets the logger name.
        /// </summary>
        /// <value>
        /// The logger name.
        /// </value>
        public string Name { get; set; }


        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the specified <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        public static ILogBuilder Log(LogLevel logLevel, [CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(logLevel, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the specified <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Log(LogLevel logLevel)
        {
            var builder = Log(logLevel);
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the computed <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevelFactory">The log level factory.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        public static ILogBuilder Log(Func<LogLevel> logLevelFactory, [CallerFilePath]string callerFilePath = null)
        {
            var logLevel = (logLevelFactory != null)
                ? logLevelFactory()
                : LogLevel.Debug;

            return CreateBuilder(logLevel, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogBuilder" /> with the computed <see cref="LogLevel" />.
        /// </summary>
        /// <param name="logLevelFactory">The log level factory.</param>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Log(Func<LogLevel> logLevelFactory)
        {
            var builder = Log(logLevelFactory);
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Trace"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Trace([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Trace, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Trace" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Trace()
        {
            var builder = Trace();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Debug"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Debug([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Debug, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Debug" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Debug()
        {
            var builder = Debug();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Info"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Info([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Info, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Info" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Info()
        {
            var builder = Info();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Warn"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Warn([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Warn, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Warn" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Warn()
        {
            var builder = Warn();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Error"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Error([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Error, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Error" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Error()
        {
            var builder = Error();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Start a fluent <see cref="LogLevel.Fatal"/> logger.
        /// </summary>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is the file path at the time of compile.</param>
        /// <returns>A fluent Logger instance.</returns>
        public static ILogBuilder Fatal([CallerFilePath]string callerFilePath = null)
        {
            return CreateBuilder(LogLevel.Fatal, callerFilePath);
        }

        /// <summary>
        /// Start a fluent <see cref="LogLevel.Fatal" /> logger.
        /// </summary>
        /// <returns>
        /// A fluent Logger instance.
        /// </returns>
        ILogBuilder ILogger.Fatal()
        {
            var builder = Fatal();
            return MergeDefaults(builder);
        }


        /// <summary>
        /// Registers a <see langword="delegate"/> to write logs to.
        /// </summary>
        /// <param name="writer">The <see langword="delegate"/> to write logs to.</param>
        public static void RegisterWriter(Action<LogData> writer)
        {
            lock (_writerLock)
            {
                _hasSearched = true;
                _logAction = writer;
            }
        }

        /// <summary>
        /// Registers a ILogWriter to write logs to.
        /// </summary>
        /// <param name="writer">The ILogWriter to write logs to.</param>
        public static void RegisterWriter<TWriter>(TWriter writer)
            where TWriter : ILogWriter
        {
            lock (_writerLock)
            {
                _hasSearched = true;
                _logWriter = writer;
            }
        }


        /// <summary>
        /// Creates a new <see cref="ILogger"/> using the specified fluent <paramref name="builder"/> action.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <returns></returns>
        public static ILogger CreateLogger(Action<LoggerCreateBuilder> builder)
        {
            var factory = new Logger();
            var factoryBuilder = new LoggerCreateBuilder(factory);

            builder(factoryBuilder);

            return factory;
        }

        /// <summary>
        /// Creates a new <see cref="ILogger"/> using the caller file name as the logger name.
        /// </summary>
        /// <returns></returns>
        public static ILogger CreateLogger([CallerFilePath]string callerFilePath = null)
        {
            return new Logger { Name = GetName(callerFilePath) };
        }

        /// <summary>
        /// Creates a new <see cref="ILogger" /> using the specified type as the logger name.
        /// </summary>
        /// <param name="type">The type to use as the logger name.</param>
        /// <returns></returns>
        public static ILogger CreateLogger(Type type)
        {
            return new Logger { Name = type.FullName };
        }

        /// <summary>
        /// Creates a new <see cref="ILogger" /> using the specified type as the logger name.
        /// </summary>
        /// <typeparam name="T">The type to use as the logger name.</typeparam>
        /// <returns></returns>
        public static ILogger CreateLogger<T>()
        {
            return CreateLogger(typeof(T));
        }


        private static Action<LogData> ResolveWriter()
        {
            lock (_writerLock)
            {
                SearchWriter();
                if (_logWriter != null)
                    return _logWriter.WriteLog;

                return _logAction ?? DebugWrite;
            }
        }

        private static void SearchWriter()
        {
            if (_hasSearched)
                return;

            _hasSearched = true;

            //search all assemblies for ILogWriter
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var a in assemblies)
            {
                Type[] types;

                try
                {
                    types = a.GetExportedTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                var writerType = typeof(ILogWriter);
                var type = types.FirstOrDefault(t => !t.IsAbstract && writerType.IsAssignableFrom(t));
                if (type == null)
                    continue;

                _logWriter = Activator.CreateInstance(type) as ILogWriter;
                return;
            }
        }

        private static void DebugWrite(LogData logData)
        {
            System.Diagnostics.Debug.WriteLine(logData);
        }

        private static ILogBuilder CreateBuilder(LogLevel logLevel, string callerFilePath)
        {
            string name = GetName(callerFilePath);

            var writer = ResolveWriter();
            var builder = new LogBuilder(logLevel, writer);
            builder.Logger(name);

            MergeProperties(builder);

            return builder;
        }

        private static string GetName(string path)
        {
            if (path == null)
                return string.Empty;

            var parts = path.Split('\\', '/');
            var p = parts.LastOrDefault();
            if (p == null)
                return null;

            int length;
            if ((length = p.LastIndexOf('.')) == -1)
                return p;

            return p.Substring(0, length);
        }

        private static IPropertyContext CreateAsync()
        {
            var propertyContext = new AsynchronousContext();
            return propertyContext;
        }

        private static IPropertyContext CreateLocal()
        {
            var propertyContext = new PropertyContext();
            propertyContext.Set("ThreadId", Thread.CurrentThread.ManagedThreadId);

            return propertyContext;
        }

        private static IPropertyContext CreateGlobal()
        {
            var propertyContext = new PropertyContext();
            propertyContext.Set("MachineName", Environment.MachineName);

            return propertyContext;
        }

        private static void MergeProperties(ILogBuilder builder)
        {
            // copy global properties to current builder only if it has been created
            if (_globalProperties.IsValueCreated)
                _globalProperties.Value.Apply(builder);

            // copy thread-local properties to current builder only if it has been created
            if (_threadProperties.IsValueCreated)
                _threadProperties.Value.Apply(builder);

            // copy async properties to current builder only if it has been created
            if (_asyncProperties.IsValueCreated)
                _asyncProperties.Value.Apply(builder);
        }


        private ILogBuilder MergeDefaults(ILogBuilder builder)
        {
            // copy logger name
            if (!String.IsNullOrEmpty(Name))
                builder.Logger(Name);

            // copy properties to current builder
            if (_properties.IsValueCreated)
                _properties.Value.Apply(builder);

            return builder;
        }
    }

    /// <summary>
    /// A class holding log data before being written.
    /// </summary>
    public sealed class LogData
    {
        /// <summary>
        /// Gets or sets the logger name.
        /// </summary>
        /// <value>
        /// The logger name.
        /// </value>
        public string Logger { get; set; }

        /// <summary>
        /// Gets or sets the trace level.
        /// </summary>
        /// <value>
        /// The trace level.
        /// </value>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>
        /// The message.
        /// </value>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the message parameters. Used with String.Format.
        /// </summary>
        /// <value>
        /// The parameters.
        /// </value>
        public object[] Parameters { get; set; }

        /// <summary>
        /// Gets or sets the format provider.
        /// </summary>
        /// <value>
        /// The format provider.
        /// </value>
        public IFormatProvider FormatProvider { get; set; }

        /// <summary>
        /// Gets or sets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the name of the member.
        /// </summary>
        /// <value>
        /// The name of the member.
        /// </value>
        public string MemberName { get; set; }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        /// <value>
        /// The file path.
        /// </value>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the line number.
        /// </summary>
        /// <value>
        /// The line number.
        /// </value>
        public int LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the log properties.
        /// </summary>
        /// <value>
        /// The log properties.
        /// </value>
        public IDictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var message = new StringBuilder();
            message
                .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                .Append(" [")
                .Append(LogLevel.ToString()[0])
                .Append("] ");

            if (!string.IsNullOrEmpty(FilePath) && !string.IsNullOrEmpty(MemberName))
            {
                message
                    .Append("[")
                    .Append(FilePath)
                    .Append(" ")
                    .Append(MemberName)
                    .Append("()")
                    .Append(" Ln: ")
                    .Append(LineNumber)
                    .Append("] ");
            }

            if (Parameters != null && Parameters.Length > 0)
                message.AppendFormat(FormatProvider, Message, Parameters);
            else
                message.Append(Message);

            if (Exception != null)
                message.Append(" ").Append(Exception);

            return message.ToString();
        }
    }

    /// <summary>
    /// A fluent <see langword="interface"/> to build log messages.
    /// </summary>
    public interface ILogBuilder
    {
        /// <summary>
        /// Gets the log data that is being built.
        /// </summary>
        /// <value>
        /// The log data.
        /// </value>
        LogData LogData { get; }

        /// <summary>
        /// Sets the level of the logging event.
        /// </summary>
        /// <param name="logLevel">The level of the logging event.</param>
        /// <returns></returns>
        ILogBuilder Level(LogLevel logLevel);

        /// <summary>
        /// Sets the logger for the logging event.
        /// </summary>
        /// <param name="logger">The name of the logger.</param>
        /// <returns></returns>
        ILogBuilder Logger(string logger);

        /// <summary>
        /// Sets the logger name using the generic type.
        /// </summary>
        /// <typeparam name="TLogger">The type of the logger.</typeparam>
        /// <returns></returns>
        ILogBuilder Logger<TLogger>();

        /// <summary>
        /// Sets the log message on the logging event.
        /// </summary>
        /// <param name="message">The log message for the logging event.</param>
        /// <returns></returns>
        ILogBuilder Message(string message);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The object to format.</param>
        /// <returns></returns>
        ILogBuilder Message(string format, object arg0);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <returns></returns>
        ILogBuilder Message(string format, object arg0, object arg1);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        /// <returns></returns>
        ILogBuilder Message(string format, object arg0, object arg1, object arg2);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        /// <param name="arg3">The fourth object to format.</param>
        /// <returns></returns>
        ILogBuilder Message(string format, object arg0, object arg1, object arg2, object arg3);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <returns></returns>
        ILogBuilder Message(string format, params object[] args);

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <returns></returns>
        ILogBuilder Message(IFormatProvider provider, string format, params object[] args);

        /// <summary>
        /// Sets a log context property on the logging event.
        /// </summary>
        /// <param name="name">The name of the context property.</param>
        /// <param name="value">The value of the context property.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        ILogBuilder Property(string name, object value);

        /// <summary>
        /// Sets the exception information of the logging event.
        /// </summary>
        /// <param name="exception">The exception information of the logging event.</param>
        /// <returns></returns>
        ILogBuilder Exception(Exception exception);

        /// <summary>
        /// Writes the log event to the underlying logger.
        /// </summary>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        void Write(
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0);

        /// <summary>
        /// Writes the log event to the underlying logger if the condition delegate is true.
        /// </summary>
        /// <param name="condition">If condition is true, write log event; otherwise ignore event.</param>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        void WriteIf(
            Func<bool> condition,
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0);

        /// <summary>
        /// Writes the log event to the underlying logger if the condition is true.
        /// </summary>
        /// <param name="condition">If condition is true, write log event; otherwise ignore event.</param>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        void WriteIf(
            bool condition,
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0);
    }

    /// <summary>
    /// A fluent <see langword="interface"/> to build log messages.
    /// </summary>
    public sealed class LogBuilder : ILogBuilder
    {
        private readonly LogData _data;
        private readonly Action<LogData> _writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogBuilder" /> class.
        /// </summary>
        /// <param name="logLevel">The starting trace level.</param>
        /// <param name="writer">The delegate to write logs to.</param>
        /// <exception cref="System.ArgumentNullException">writer</exception>
        public LogBuilder(LogLevel logLevel, Action<LogData> writer)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");

            _writer = writer;
            _data = new LogData();
            _data.LogLevel = logLevel;
            _data.FormatProvider = CultureInfo.InvariantCulture;
            _data.Logger = typeof(Logger).FullName;
        }

        /// <summary>
        /// Gets the log data that is being built.
        /// </summary>
        /// <value>
        /// The log data.
        /// </value>
        public LogData LogData
        {
            get { return _data; }
        }

        /// <summary>
        /// Sets the level of the logging event.
        /// </summary>
        /// <param name="logLevel">The level of the logging event.</param>
        /// <returns></returns>
        public ILogBuilder Level(LogLevel logLevel)
        {
            _data.LogLevel = logLevel;
            return this;
        }

        /// <summary>
        /// Sets the logger for the logging event.
        /// </summary>
        /// <param name="logger">The name of the logger.</param>
        /// <returns></returns>
        public ILogBuilder Logger(string logger)
        {
            _data.Logger = logger;

            return this;
        }

        /// <summary>
        /// Sets the logger name using the generic type.
        /// </summary>
        /// <typeparam name="TLogger">The type of the logger.</typeparam>
        /// <returns></returns>
        public ILogBuilder Logger<TLogger>()
        {
            _data.Logger = typeof(TLogger).FullName;

            return this;
        }

        /// <summary>
        /// Sets the log message on the logging event.
        /// </summary>
        /// <param name="message">The log message for the logging event.</param>
        /// <returns></returns>
        public ILogBuilder Message(string message)
        {
            _data.Message = message;

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The object to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(string format, object arg0)
        {
            _data.Message = format;
            _data.Parameters = new[] { arg0 };

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(string format, object arg0, object arg1)
        {
            _data.Message = format;
            _data.Parameters = new[] { arg0, arg1 };

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(string format, object arg0, object arg1, object arg2)
        {
            _data.Message = format;
            _data.Parameters = new[] { arg0, arg1, arg2 };

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="arg0">The first object to format.</param>
        /// <param name="arg1">The second object to format.</param>
        /// <param name="arg2">The third object to format.</param>
        /// <param name="arg3">The fourth object to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(string format, object arg0, object arg1, object arg2, object arg3)
        {
            _data.Message = format;
            _data.Parameters = new[] { arg0, arg1, arg2, arg3 };

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(string format, params object[] args)
        {
            _data.Message = format;
            _data.Parameters = args;

            return this;
        }

        /// <summary>
        /// Sets the log message and parameters for formating on the logging event.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An object array that contains zero or more objects to format.</param>
        /// <returns></returns>
        public ILogBuilder Message(IFormatProvider provider, string format, params object[] args)
        {
            _data.FormatProvider = provider;
            _data.Message = format;
            _data.Parameters = args;

            return this;
        }

        /// <summary>
        /// Sets a log context property on the logging event.
        /// </summary>
        /// <param name="name">The name of the context property.</param>
        /// <param name="value">The value of the context property.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public ILogBuilder Property(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (_data.Properties == null)
                _data.Properties = new Dictionary<string, object>();

            _data.Properties[name] = value;
            return this;
        }

        /// <summary>
        /// Sets the exception information of the logging event.
        /// </summary>
        /// <param name="exception">The exception information of the logging event.</param>
        /// <returns></returns>
        public ILogBuilder Exception(Exception exception)
        {
            _data.Exception = exception;
            return this;
        }

        /// <summary>
        /// Writes the log event to the underlying logger.
        /// </summary>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        public void Write(
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0)
        {
            if (callerMemberName != null)
                _data.MemberName = callerMemberName;
            if (callerFilePath != null)
                _data.FilePath = callerFilePath;
            if (callerLineNumber != 0)
                _data.LineNumber = callerLineNumber;

            _writer(_data);
        }


        /// <summary>
        /// Writes the log event to the underlying logger if the condition delegate is true.
        /// </summary>
        /// <param name="condition">If condition is true, write log event; otherwise ignore event.</param>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        public void WriteIf(
            Func<bool> condition,
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0)
        {
            if (condition == null || !condition())
                return;

            Write(callerMemberName, callerFilePath, callerLineNumber);
        }

        /// <summary>
        /// Writes the log event to the underlying logger if the condition is true.
        /// </summary>
        /// <param name="condition">If condition is true, write log event; otherwise ignore event.</param>
        /// <param name="callerMemberName">The method or property name of the caller to the method. This is set at by the compiler.</param>
        /// <param name="callerFilePath">The full path of the source file that contains the caller. This is set at by the compiler.</param>
        /// <param name="callerLineNumber">The line number in the source file at which the method is called. This is set at by the compiler.</param>
        public void WriteIf(
            bool condition,
            [CallerMemberName]string callerMemberName = null,
            [CallerFilePath]string callerFilePath = null,
            [CallerLineNumber]int callerLineNumber = 0)
        {
            if (!condition)
                return;

            Write(callerMemberName, callerFilePath, callerLineNumber);
        }

    }

    /// <summary>
    /// A class that will call an <see cref="Action"/> when Disposed.
    /// </summary>
    public class DisposeAction : IDisposable
    {
        private readonly Action _exitAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisposeAction"/> class.
        /// </summary>
        /// <param name="exitAction">The exit action.</param>
        public DisposeAction(Action exitAction)
        {
            _exitAction = exitAction;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        void IDisposable.Dispose()
        {
            _exitAction.Invoke();
        }
    }

    /// <summary>
    /// An interface defining a log writer.
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// Writes the specified LogData to the underlying logger.
        /// </summary>
        /// <param name="logData">The log data.</param>
        void WriteLog(LogData logData);
    }


    /// <summary>
    /// A fluent class to build a <see cref="LogFactory"/>.
    /// </summary>
    public class LoggerCreateBuilder
    {
        private readonly Logger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerCreateBuilder"/> class.
        /// </summary>
        /// <param name="logger">The factory.</param>
        public LoggerCreateBuilder(Logger logger)
        {
            _logger = logger;
        }


        /// <summary>
        /// Sets the initial logger name for the logging event.
        /// </summary>
        /// <param name="logger">The name of the logger.</param>
        /// <returns></returns>
        public LoggerCreateBuilder Logger(string logger)
        {
            _logger.Name = logger;

            return this;
        }

        /// <summary>
        /// Sets the initial logger name using the generic type.
        /// </summary>
        /// <typeparam name="TLogger">The type of the logger.</typeparam>
        /// <returns></returns>
        public LoggerCreateBuilder Logger<TLogger>()
        {
            _logger.Name = typeof(TLogger).FullName;

            return this;
        }

        /// <summary>
        /// Sets the initial logger name using the specified type.
        /// </summary>
        /// <param name="type">The type of the logger.</param>
        /// <returns></returns>
        public LoggerCreateBuilder Logger(Type type)
        {
            _logger.Name = type.FullName;

            return this;
        }


        /// <summary>
        /// Sets an initial  log context property on the logging event.
        /// </summary>
        /// <param name="name">The name of the context property.</param>
        /// <param name="value">The value of the context property.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">name</exception>
        public LoggerCreateBuilder Property(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            _logger.Properties.Set(name, value);
            return this;
        }
    }

    /// <summary>
    /// An <see langword="interface"/> defining a logger property context.
    /// </summary>
    public interface IPropertyContext
    {
        /// <summary>
        /// Applies the context properties to the specified <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The builder to copy the properties to.</param>
        void Apply(ILogBuilder builder);

        /// <summary>
        /// Removes all keys and values from the property context
        /// </summary>
        void Clear();

        /// <summary>
        /// Determines whether the property context contains the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key to locate in the property context.</param>
        /// <returns><c>true</c> if the property context contains an element with the specified <paramref name="key"/>; otherwise, <c>false</c>.</returns>
        bool Contains(string key);

        /// <summary>
        /// Gets the value associated with the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The value associated with the specified <paramref name="key"/>, if the key is found; otherwise <see langword="null"/>.</returns>
        object Get(string key);

        /// <summary>
        /// Gets the keys in the property context.
        /// </summary>
        /// <returns>The keys in the property context.</returns>
        IEnumerable<string> Keys();

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.</returns>
        bool Remove(string key);

        /// <summary>
        /// Sets the <paramref name="value"/> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key. The value will be converted to a string.</param>
        void Set(string key, object value);

        /// <summary>
        /// Sets the <paramref name="value"/> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key. The value will be converted to a string.</param>
        /// <returns>An <see cref="IDisposable"/> that will remove the key on dispose.</returns>
        IDisposable Push(string key, object value);

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.</returns>
        object Pop(string key);
    }

    /// <summary>
    /// A property context that maintains state across asynchronous tasks and call contexts.
    /// </summary>
    public class AsynchronousContext : IPropertyContext
    {
        private readonly string _slotName = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Applies the context properties to the specified <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">The builder to copy the properties to.</param>
        public void Apply(ILogBuilder builder)
        {
            var dictionary = GetDictionary();
            if (dictionary == null)
                return;

            foreach (var pair in dictionary)
                builder.Property(pair.Key, pair.Value);
        }

        /// <summary>
        /// Removes all keys and values from the property context
        /// </summary>
        public void Clear()
        {
            CallContext.FreeNamedDataSlot(_slotName);
        }

        /// <summary>
        /// Determines whether the property context contains the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key to locate in the property context.</param>
        /// <returns>
        ///   <c>true</c> if the property context contains an element with the specified <paramref name="key" />; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(string key)
        {
            var dictionary = GetDictionary();
            if (dictionary == null)
                return false;

            return dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Gets the value associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The value associated with the specified <paramref name="key" />, if the key is found; otherwise <see langword="null" />.
        /// </returns>
        public object Get(string key)
        {
            var dictionary = GetDictionary();
            if (dictionary == null)
                return null;

            object value;
            dictionary.TryGetValue(key, out value);

            return value;
        }

        /// <summary>
        /// Gets the keys in the property context.
        /// </summary>
        /// <returns>
        /// The keys in the property context.
        /// </returns>
        public IEnumerable<string> Keys()
        {
            var dictionary = GetDictionary();
            if (dictionary == null)
                return Enumerable.Empty<string>();

            return dictionary.Keys;
        }

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        ///   <c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.
        /// </returns>
        public bool Remove(string key)
        {
            var dictionary = GetDictionary();
            if (dictionary == null)
                return false;

            bool removed = dictionary.Remove(key);

            // CallContext value must be immutable, reassign value
            if (dictionary.Count > 0)
                SetDictionary(dictionary);
            else
                Clear();

            return removed;
        }

        /// <summary>
        /// Sets the <paramref name="value" /> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key.</param>
        public void Set(string key, object value)
        {
            var dictionary = GetDictionary()
                ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            dictionary[key] = value;

            // CallContext value must be immutable, reassign value
            SetDictionary(dictionary);
        }

        /// <summary>
        /// Sets the <paramref name="value" /> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key.</param>
        /// <returns>
        /// An <see cref="IDisposable" /> that will remove the key on dispose.
        /// </returns>
        public IDisposable Push(string key, object value)
        {
            Set(key, value);

            return new DisposeAction(() => Remove(key));
        }

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        ///   <c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.
        /// </returns>
        public object Pop(string key)
        {
            var value = Get(key);
            Remove(key);
            return value;
        }


        private IDictionary<string, object> GetDictionary()
        {
            var data = CallContext.LogicalGetData(_slotName);
            return data as IDictionary<string, object>;
        }

        private void SetDictionary(IDictionary<string, object> value)
        {
            CallContext.LogicalSetData(_slotName, value);
        }
    }

    /// <summary>
    /// A property context that maintains state in a local dictionary
    /// </summary>
    public class PropertyContext : IPropertyContext
    {
        private readonly Dictionary<string, object> _dictionary;

        /// <summary>
        /// Applies the context properties to the specified <paramref name="builder" />.
        /// </summary>
        /// <param name="builder">The builder to copy the properties to.</param>
        public void Apply(ILogBuilder builder)
        {
            foreach (var pair in _dictionary)
                builder.Property(pair.Key, pair.Value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyContext"/> class.
        /// </summary>
        public PropertyContext()
        {
            _dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes all keys and values from the property context
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
        }

        /// <summary>
        /// Determines whether the property context contains the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key to locate in the property context.</param>
        /// <returns>
        ///   <c>true</c> if the property context contains an element with the specified <paramref name="key" />; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(string key)
        {
            return _dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Gets the value associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>
        /// The value associated with the specified <paramref name="key" />, if the key is found; otherwise <see langword="null" />.
        /// </returns>
        public object Get(string key)
        {
            object value;
            _dictionary.TryGetValue(key, out value);
            return value;
        }

        /// <summary>
        /// Gets the keys in the property context.
        /// </summary>
        /// <returns>
        /// The keys in the property context.
        /// </returns>
        public IEnumerable<string> Keys()
        {
            return _dictionary.Keys;
        }

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        ///   <c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.
        /// </returns>
        public bool Remove(string key)
        {
            return _dictionary.Remove(key);
        }

        /// <summary>
        /// Sets the <paramref name="value" /> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key.</param>
        /// <returns>
        /// An <see cref="IDisposable" /> that will remove the key on dispose.
        /// </returns>
        public void Set(string key, object value)
        {
            _dictionary[key] = value;
        }

        /// <summary>
        /// Sets the <paramref name="value" /> associated with the specified <paramref name="key" />.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The value associated with the specified key.</param>
        /// <returns>
        /// An <see cref="IDisposable" /> that will remove the key on dispose.
        /// </returns>
        public IDisposable Push(string key, object value)
        {
            Set(key, value);
            return new DisposeAction(() => Remove(key));
        }

        /// <summary>
        /// Removes the value with the specified <paramref name="key" /> from the property context.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        ///   <c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>. This method returns <c>false</c> if key is not found.
        /// </returns>
        public object Pop(string key)
        {
            var value = Get(key);
            Remove(key);
            return value;
        }
    }
}
