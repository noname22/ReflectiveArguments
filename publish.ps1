Select-Xml -Path .\ReflectiveArguments\ReflectiveArguments.csproj -XPath '/Project/PropertyGroup/Version' | ForEach-Object { $version = $_.Node.InnerXML }
dotnet pack -c Release
dotnet nuget push ReflectiveArguments\bin\Release\ReflectiveArguments.$version.nupkg --api-key $args[0] --source https://api.nuget.org/v3/index.json