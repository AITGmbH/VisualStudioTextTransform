# Transform T4 Templates

## The Problem:

Basically we want to transform our T4 templates as part of the build, but still use the "Host" variable.
A stackoverflow answer reveals that this is not possible out-of-the-box:
http://stackoverflow.com/questions/14409368/t4-template-will-not-transform-with-build/14409896#14409896

However there is one way to write a custom `TextTransform.exe` in a way to provide the neccessary services to the T4 templates.

## The Solution

### The Technologies

#### Custom `TextTransform.exe`.

You can actually create a modified template host with by implementing the ITextTemplatingEngineHost interface.

 - https://msdn.microsoft.com/en-us/library/bb126519.aspx
 - https://msdn.microsoft.com/en-us/library/microsoft.visualstudio.texttemplating.itexttemplatingenginehost%28v=vs.110%29.aspx

#### EnvDTE.DTE interface.

Some text templates use the "Automation and Extensibility for Visual Studio"-API (EnvDTE.DTE interface).
So in order to use those templates you need to provide the DTE interfacec from within your custom host as well.
To provide a DTE instance to the template we obviously need to either create an instance or get one from an exisiting Visual Studio instance.

- http://www.viva64.com/en/b/0169/
- https://msdn.microsoft.com/en-us/library/envdte.dte.aspx

### Combining the technologies

We can now combine the two technologies to write a custom text template processor, 
which provides exactly the same behaviour as the Visual Studio Text template processor.

First we create a custom T4 template host and implement the ITextTemplatingEngineHost interface 
almost exactly as given by the microsoft sample above.
The differences are:

 - We additionally implement `IServiceProvider` to provide the `EnvDTE80.DTE2`/`EnvDTE.DTE` instance
 - We replace `$(ProjectDir)` and `$(TargetDir)` with the respective paths (read from the `dte` instance),
   because some of our text templates expected those paths.
   
We use the same MessageFilter class as suggested by http://www.viva64.com/en/b/0169/ to automatically retry on `COMException`s.

Finally we create our own `EnvDTE.DTE` instance, iterate over all text templating file and process them with our new shiny host.

You can find the (F#) implementation on GitHub: <!insert!>

## Usage

 TODO


