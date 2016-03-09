# Fluent.Logger

Simple logger abstraction as a NuGet source code package.

[![Build status](https://ci.appveyor.com/api/projects/status/24o8k3nn3skd3hxc?svg=true)](https://ci.appveyor.com/project/LoreSoft/simple-logger)

[![NuGet Version](https://img.shields.io/nuget/v/Fluent.Logger.svg?style=flat-square)](http://www.nuget.org/packages/Fluent.Logger/)

[![NuGet Version](https://img.shields.io/nuget/dt/Fluent.Logger.svg?style=flat-square)](http://www.nuget.org/packages/Fluent.Logger/)

## Download

The Fluent.Logger library is available on nuget.org via package name `Fluent.Logger`.

To install Fluent.Logger, run the following command in the Package Manager Console

    PM> Install-Package Fluent.Logger
    
More information about NuGet package avaliable at
<https://nuget.org/packages/Fluent.Logger>

## Development Builds


Development builds are available on the myget.org feed.  A development build is promoted to the main NuGet feed when it's determined to be stable. 

In your Package Manager settings add the following package source for development builds:
<http://www.myget.org/F/loresoft/>

## Usage

Writing info message via fluent API.

    Logger.Info()
        .Message("This is a test fluent message '{0}'.", DateTime.Now.Ticks)
        .Property("Test", "InfoWrite")
        .Write();

Writing error message.

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
    
Using thread-local properties.

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

Class named logger

    public class UserManager
    {
        private static readonly ILogger _logger = Logger.CreateLogger<UserManager>();
    }


## NLog Integration

To intergrate Fluent.Logger with NLog, install the `Fluent.Logger.NLog` source code nugget package.  

    PM> Install-Package Fluent.Logger.NLog

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(NLogWriter.WriteLog);

## log4net Integration

To intergrate Fluent.Logger with log4net, install the `Fluent.Logger.log4net` source code nugget package.  

    PM> Install-Package Fluent.Logger.log4net

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(Log4NetWriter.WriteLog);
