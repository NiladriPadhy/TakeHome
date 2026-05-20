# Scenario
We build desktop software that integrates with medical diagnostic devices (vitals, ECG, spirometry). Today, several desktop apps (WPF) exchange data via local REST endpoints. We’re migrating from .NET Framework to modern .NET and stronger API contracts.  The provided Visual Studio solution is a simplified version of our software meant to illustrate proper implementation and good software engineering practices.  We have already updated the version of .NET to .NET 8 but need your help to modernize the architecture and maintainability of the system.

# Assignment

The team needs you to showcase how to
1.	Use MVVM (bindings, commands), DI, and an async HTTP client in our DesktopApp
1.	Move the domain logic out of our IngestionApi to a class library called Domain so it can be reused as the solution evolves
1.	Integration test the endpoints in our IngestionApi
1.	Unit test where applicable

# Further Context
We value automated testing across unit and integration levels for this effort, meaning E2E tests are optional.
Any additional architectural enhancements/refactoring that improve the performance, maintainability, or usability of the software are welcome.

