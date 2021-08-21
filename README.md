# .NET
.NET client SDK for KubeMQ. Simple interface to work with the KubeMQ server.

## Install KubeMQ Community Edition
Please visit [KubeMQ Community](https://github.com/kubemq-io/kubemq-community) for intallation steps.

## Install CSharp SDK

Install using Nuget 

Kubemq : https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-in-visual-studio

## Cookbook
Please visit [CSharp SDK Cookbook](https://github.com/kubemq-io/csharp-sdk-cookbook)

## General SDK description
The SDK implements all communication patterns available through the KubeMQ server:
- Events
- EventStore
- Command
- Query
- Queue

### Framework Support

- .NET Framework 4.6.1
- .NET Framework 4.7.1
- .NET Standard 2.0


## Configuration
The only required configuration setting is the KubeMQ server address.

Configuration can be set by using one of the following:
- Environment Variable
- `appsettings.json` file
- `app.Config` or `Web.config` file
- Within the code


### Configuration via Environment Variable
Set `KubeMQServerAddress` to the KubeMQ Server Address


### Configuration via appsettings.json
Add the following to your appsettings.json:
```json
{
  "KubeMQ": {
    "serverAddress": "{YourServerAddress}:{YourServerPort}"
  }
}
```


### Configuration via app.Config
Simply add the following to your app.config:
```xml
<configuration>  
   <configSections>  
    <section name="KubeMQ" type="System.Configuration.NameValueSectionHandler"/>      
  </configSections>  
    
  <KubeMQ>  
    <add key="serverAddress" value="{YourServerAddress}:{YourServerPort}"/>
  </KubeMQ>  
</configuration>
```



