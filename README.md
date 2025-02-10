# ytbackground-backend-frontend


This repository contains the source code for the `ytbackground-backend-frontend` project, which includes both the backend and frontend components. The backend is built using ASP.NET Core, and the frontend is built using Angular.

## Table of Contents

- [Backend](#backend)
  - [Technologies](#technologies)
  - [Setup](#setup)
  - [Running the Application](#running-the-application)
- [Frontend](#frontend)
  - [Technologies](#technologies-1)
  - [Setup](#setup-1)
  - [Running the Application](#running-the-application-1)
- [Docker](#docker)
  - [Setup](#setup-2)
  - [Running the Application](#running-the-application-2)

## Backend

The backend is built using ASP.NET Core and provides APIs for interacting with YouTube videos.

### Technologies

- ASP.NET Core 6.0
- YoutubeExplode
- Swashbuckle.AspNetCore (for Swagger)

### Setup

1. **Install .NET SDK:**
   - Ensure you have the .NET SDK installed. You can download it from the [official .NET website](https://dotnet.microsoft.com/download).

2. **Restore Dependencies:**
   - Navigate to the backend project directory and restore the dependencies:
     ```sh
     cd backend
     dotnet restore
     ```

### Running the Application

1. **Build and Run:**
   - Build and run the backend application:
     ```sh
     dotnet run
     ```

2. **Swagger UI:**
   - Once the application is running, you can access the Swagger UI at `http://localhost:5000/swagger` to explore the available APIs.

## Frontend

The frontend is built using Angular and provides a user interface for interacting with YouTube videos.
Search by text and video id. Play only audio.

### Technologies

- Angular
- Bootstrap

### Setup

1. **Install Node.js and npm:**
   - Ensure you have Node.js and npm installed. You can download them from the [official Node.js website](https://nodejs.org/).

2. **Install Angular CLI:**
   - Install the Angular CLI globally:
     ```sh
     npm install -g @angular/cli
     ```

3. **Install Dependencies:**
   - Navigate to the frontend project directory and install the dependencies:
     ```sh
     cd frontend
     npm install
     ```

### Running the Application

1. **Serve the Application:**
   - Serve the frontend application:
     ```sh
     ng serve
     ```

2. **Access the Application:**
   - Once the application is running, you can access it at `http://localhost:4200`.

## Docker

You can use Docker to containerize both the backend and frontend applications.

### Setup

1. **Install Docker:**
   - Ensure you have Docker installed. You can download it from the [official Docker website](https://www.docker.com/get-started).

2. **Create Docker Images:**
   - Navigate to the root of the project directory and build the Docker images using Docker Compose:
     ```sh
     docker-compose build
     ```

### Running the Application

1. **Start the Containers:**
   - Start the backend and frontend containers using Docker Compose:
     ```sh
     docker-compose up
     ```

2. **Access the Applications:**
   - Once the containers are running, you can access the frontend application at `http://localhost:4200` and the backend application at `http://localhost:5054`.

## License

This project is licensed under the MIT License. See the [LICENSE](http://_vscodecontentref_/1) file for more details.