# Audify

Personal self-learning project. Audify parses your exported Spotify streaming JSON files and shows local listening statistics in a simple local dashboard.

This repository started as generated/example code to serve as a base for future learning: Docker, CI/CD and GitHub Actions, databases & migrations, authentication, deployment workflows, and related topics.

Quick start

- Open the solution in Visual Studio and run the `Audify.Api` project, or run:

  ```powershell
  dotnet run --project src\Audify.Api
  ```

Notes

- This project is for experimentation and learning only; it is not intended for production use.
- Uploaded Spotify JSON is processed in-memory by the API and not persisted by default.
