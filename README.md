#ASP.NET vNext Preview

In the next version of ASP.NET we are working with multiple teams around Microsoft to create a lean, composable .NET stack that provides a familiar and modern framework for web and cloud scenarios.

The Home repository is the starting point for people to learn about ASP.NET vNext, it contains samples and [documentation](https://github.com/aspnet/Home/wiki) to help folks get started and learn more about what we are doing.
The Home repository is the starting point for people to learn about ASP.NET vNext, it contains samples and [documentation](https://github.com/aspnet/Home/wiki) to help folks get started and learn more about what we are doing.

The samples provided are designed to show some of the features of the new framework as well as setting up a sandbox for you to try out some of the new features. The NuGet.config file in the repo points to a MyGet feed that has all the packages being developed. The feed is updated every time a full build succeeds.

#Getting Started

The first thing we need to do is setup the tools required to build and run an application. We will start out by getting the [K Version Manager (KVM)](https://github.com/aspnet/Home/wiki/version-manager)

* Clone the repository
* On the command line execute ```kvmsetup.cmd``` 
* This command will setup your environment, getting it ready to install a version of the runtime. It adds kvm to your path and puts it in your user profile.
* Execute ```kvm upgrade```. This command will download the latest version of the SDK and put it on your user profile ready to use. You could also install a specific version here instead of getting the latest, see the [KVM page](https://github.com/aspnet/Home/wiki/version-manager)
* Navigate to samples\HelloConsole
* Run ```K run```
* You should see some compiler output and a message saying "Hello World"

#Samples

##Sandbox Samples

These samples, in this repo, are just basic starting points for you to experiment with features. Since there is no File->New Project we thought some simple samples to take the place of scaffolding would be convenient.

+ [ConsoleApp](https://github.com/aspnet/Home/tree/master/samples/ConsoleApp). This is just basic console app if you want to use it as a starting point.
+ [HelloWeb](https://github.com/aspnet/Home/tree/master/samples/HelloWeb). This is a minimal startup class that shows welcome page and static file middleware. This is mostly for you to run through the steps in the readme and make sure you have everything setup and working correctly.
+ [HelloWebFx](https://github.com/aspnet/Home/tree/master/samples/HelloWebFx). This sample is a basic MVC app. It is not designed to show all the functionality of the new web stack, but to give you a starting point to play with features.

**NOTE: The samples are pinned to a specific version of the packages. If you want to try the latest builds then update the project.json and replace the last part of the version with a '\*', so '0.1-alpha-build-267' becomes '0.1-alpha-\*', and then run ```kpm restore``` to pull down the latest packages**

##Feature Samples
The [Entropy repo](https://github.com/aspnet/Entropy) contains samples of specific features in isolation. Each directory contains just enough code to show an aspect of a feature.

##Application Samples
[MVC Music Store](https://github.com/aspnet/MusicStore) and [BugTracker](https://github.com/aspnet/BugTracker) application are both being ported. Each of these have their own repository that you can look at.

#Running the samples

##Running HelloWeb

1. Clone the repository
2. Change directory to Samples\HelloWeb
3. Run ```kpm restore```
4. You should see a bunch of output as all the dependencies of the app are downloaded from MyGet. The K commands all operate on the app that is in the current directory.
5. Run ```K web```
6. You should see build output and a message to show the site is now started
7. Navigate to "http://localhost:5001"
8. You should see the welcome page
9. Navigate to "http://localhost:5001/image.jpg"
10. You should see an image served with the static file middleware

If you can do all of the above then everything should be working. You can try out the WebFx sample now to see some more of the new stack. You should run ```kpm restore``` before using any sample for the first time.

#Switching to Core CLR

By default when running the applications you are running against Desktop CLR (4.5), you can change that by setting the TARGET_FRAMEWORK variable:

1. Run ```set TARGET_FRAMEWORK=k10```
2. Run ```K web```
3. The first line of your output should say "Loaded Module: klr.core45.dll" instead of "Loaded Module: klr.net45.dll"
4. The HelloWeb app should work the same as when running on Desktop CLR.

**NOTE: There are going to be parts of the stack that work on Desktop but do not work on Core CLR. This set should get smaller and smaller as time goes on, but it is entirely likely as you use Core CLR you will hit errors that can't be worked around as the Core CLR surface area just does not exist yet. An example of this type of problem is using Entity Framework with a database. There are not currently any real database providers that work on Core CLR, so you will be restricted to in-memory EF on Core CLR.**

#Core CLR Packages

Currently the BCL is split into some fairly fine grained packages, which was one of the goals of this effort. However, the packages that exist today do not necessarily represent the list of packages that we will end up with. We are still experimenting with what makes sense to be a package and what the experience should be.

#OS Requirements

Today Core CLR will only work on Windows 8 and above, otherwise we support Windows 7 and above. Before RTM Core CLR will support Windows 7 as well.

#Feedback

You can log issues in this repo in order to start discussions, ask questions, make suggestions, etc.
