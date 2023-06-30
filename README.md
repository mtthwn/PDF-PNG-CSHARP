# Micro PDF to PNG conversion API

This is a sample project that uses a .NET Core (v7) API and poppler to convert uploaded PDF files to PNGs. The PNG files are returned in a `.zip` repository to the client once the conversions have completed.

## Dependencies
- .NET 7
- poppler (pdftoppm) installed locally

## Running the app
Restore all dependencies then run the app in Visual Studio or Rider. You can upload PDF files using Postman to the proper URL/PORT in a POST request.

## Future considerations
- Perform conversions in memory (currently writing files to disk and cleaning up later)
- Add async conversion (currently done synchronously)
- Implement queue system to handle multiple uploads at a time
- Breakdown of logic into separate files using OOP/MVC patterns
