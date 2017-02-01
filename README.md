# Runtime-Assembly-Resolver

### Intro
In a MEF context, you can dynamically load DLL at runtime, but I found limitations using it.
When you initialize your Dictionary of MEF dll, you have to explicitly register all directories you want to use. If one of the assemblies have a dependency on another assembly that is not in the same directory, you have a nice exception thrown.
After some investigations over the web on this issue, it seems that you manually have to resolve assemblies location when AssemblyResolve event is raised.

This lib has been made for this purpose. You can define directories to dynamically use for assembly resolution.

### How to use it

- Reference this lib in your application entry point.
- Then, in your `App.config`, add this two keys:
```xml
  <appSettings>
    <!-- In this key, list all directories you want to use. You can include subdirectories using * -->
    <add key="AssembliesSource" value="C:\MyDllSourceOne;C:\User\Bob\MyCustomDllSource\*" />
    <!-- If your application is multicultural, set the directory that contains all languages directories here -->
    <add key="LanguagesDirectories" value="C:\MyApp\languages"/>
  </appSettings>
```

- Finally, as soon as possible in your application, initialize the `AssemblyResolver`
```csharp
Okin.AssemblyResolver.Instance.Initialize();
``` 
That's it. The resolver is now registered in the current domain and will resolve assembly loading using configured paths in `App.config`.

What if you want to add a new directory later in code ?

Just call this whenever you want:
```csharp
Okin.AssemblyResolver.Instance.AddPathToLocations(Path.GetFullPath("C:\MySuperPath"));
```
As long as the resolver is initialized, if you register a new path, it will be used for resolution.

If your app is multidomain, it works too. Just call the following method with the AppDomain you want as parameter:
```csharp
Okin.AssemblyResolver.Instance.RegisterAssemblyResolverInDomain(domainToUse);
```

### Todo
- Today paths are prioritized according to their index in `AssembliesLocations`. So it means that if you want to dynamically add a new path for resolution using `AddPathToLocations`, the new path will be added at the end so be the lower priority assembly used in resolution. What if I want to use it as top priority location ? I should offer the possibility to set the priority in assemblies resolution paths.
