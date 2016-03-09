# FluentLogger

Fluent logger abstraction as a NuGet source code package.

[![Build status](https://ci.appveyor.com/api/projects/status/24o8k3nn3skd3hxc?svg=true)](https://ci.appveyor.com/project/LoreSoft/simple-logger)

[![NuGet Version](https://img.shields.io/nuget/v/FluentLogger.svg?style=flat-square)](http://www.nuget.org/packages/FluentLogger/)

[![NuGet Version](https://img.shields.io/nuget/dt/FluentLogger.svg?style=flat-square)](http://www.nuget.org/packages/FluentLogger/)

## Download

The FluentLogger library is available on nuget.org via package name `FluentLogger`.

To install FluentLogger, run the following command in the Package Manager Console

    PM> Install-Package FluentLogger
    
More information about NuGet package avaliable at
<https://nuget.org/packages/FluentLogger>

## Development Builds


Development builds are available on the myget.org feed.  A development build is promoted to the main NuGet feed when it's determined to be stable. 

In your Package Manager settings add the following package source for development builds:
<http://www.myget.org/F/loresoft/>

## Usage

Writing info message via fluent API.

```csharp
Logger.Info()
    .Message("This is a test fluent message '{0}'.", DateTime.Now.Ticks)
    .Property("Test", "InfoWrite")
    .Write();
```

Writing error message.

```csharp
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
```

Using thread-local properties.

```csharp
public bool Run(int jobId)
{
    try
    {
        Logger.ThreadProperties.Set("Job", jobId);

        // all log writes on current thread will now include a Job property
        Logger.Trace()
            .Message("Starting Work ...")
            .Write();

        // DO WORK

        return true;
    }
    catch (Exception ex)
    {
        Logger.Error()
            .Message("Error: ", ex.Message)
            .Exception(ex)
            .Write();

        return false;
    }
    finally
    {
        // clear Job property for this thread
        Logger.ThreadProperties.Remove("Job");
    }
}
```

Using async properties.

```csharp
public async Task<bool> Run(int jobId)
{
    try
    {
        Logger.AsyncProperties.Set("Job", jobId);

        return await DoWork();
    }
    catch (Exception ex)
    {
        Logger.Error()
            .Message("Error: ", ex.Message)
            .Exception(ex)
            .Write();

        return false;
    }
    finally
    {
        // clear Job property for this async context
        Logger.AsyncProperties.Remove("Job");
    }
}

public async Task<bool> DoWork()
{
    // all log writes on current async context will now include a Job property
    Logger.Trace()
        .Message("Starting Work ...")
        .Write();

    // DO WORK

    return true;
}
```

Class named logger

```csharp
public class UserManager
{
    private static readonly ILogger _logger = Logger.CreateLogger<UserManager>();
}
```

Dependency inject logger

```csharp
public class UserRepository 
{
    private readonly ILogger _logger;

    public UserRepository(ILoggerFactory<UserRepository> loggerFactory)
    {
        _logger = loggerFactory.CreateLogger();
    }
}

// example register of open generic for Autofac
builder.RegisterGeneric(typeof(LoggerFactory<>)).As(typeof(ILoggerFactory<>))
```

## NLog Integration

To intergrate FluentLogger with NLog, install the `FluentLogger.NLog` source code nugget package.  

    PM> Install-Package FluentLogger.NLog

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(NLogWriter.Default);

## log4net Integration

To intergrate FluentLogger with log4net, install the `FluentLogger.log4net` source code nugget package.  

    PM> Install-Package FluentLogger.log4net

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(Log4NetWriter.Default);
