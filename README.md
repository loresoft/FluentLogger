# Simple.Logger

Simple logger abstraction as a NuGet source code package.

[![Build status](https://ci.appveyor.com/api/projects/status/24o8k3nn3skd3hxc?svg=true)](https://ci.appveyor.com/project/LoreSoft/simple-logger)

[![NuGet Version](https://img.shields.io/nuget/v/Simple.Logger.svg?style=flat-square)](http://www.nuget.org/packages/Simple.Logger/)

[![NuGet Version](https://img.shields.io/nuget/dt/Simple.Logger.svg?style=flat-square)](http://www.nuget.org/packages/Simple.Logger/)

## Download

The Simple.Logger library is available on nuget.org via package name `Simple.Logger`.

To install Simple.Logger, run the following command in the Package Manager Console

    PM> Install-Package Simple.Logger
    
More information about NuGet package avaliable at
<https://nuget.org/packages/Simple.Logger>

## Development Builds


Development builds are available on the myget.org feed.  A development build is promoted to the main NuGet feed when it's determined to be stable. 

In your Package Manager settings add the following package source for development builds:
<http://www.myget.org/F/loresoft/>

## Fluent API

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

## NLog Integration

To intergrate Simple.Logger with NLog, install the `Simple.Logger.NLog` source code nugget package.  

    PM> Install-Package Simple.Logger.NLog

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(NLogWriter.WriteLog);

## log4net Integration

To intergrate Simple.Logger with log4net, install the `Simple.Logger.log4net` source code nugget package.  

    PM> Install-Package Simple.Logger.log4net

Then register the adapter with Logger on application startup as follows.

    Logger.RegisterWriter(Log4NetWriter.WriteLog);
