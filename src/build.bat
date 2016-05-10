@..\packages\NuGet.CommandLine.3.4.3\tools\NuGet.exe restore packages.config -PackagesDirectory "..\packages"
@msbuild dddlib.msbuild /t:Build;Test;Package