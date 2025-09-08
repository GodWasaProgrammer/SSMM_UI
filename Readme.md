# SSMM_UI

**Streamer Social Media Manager**  
*With Local RTMP server and automatic Social Media posting*

---

## Table of Contents

- [About](#about)  
- [Features](#features)  
- [Installation](#installation)  
- [Usage](#usage)  
- [Configuration](#configuration)  
- [Examples](#examples)  
- [Architecture & Tech Stack](#architecture--tech-stack)  
- [Contributing](#contributing)  
- [License](#license)  
- [Contact](#contact)

---

## About

`SSMM_UI` (Streamer Social Media Manager) is a tool designed for streamers to handle broadcasting workflows and automate posting to social media platforms.  
It runs with a local RTMP server and can automatically publish to multiple platforms, removing manual steps from your workflow.

---

## Features

- **Local RTMP server** – for ingesting and handling live streams.  
- **Automatic social media posting** – cross-post to YouTube, X (Twitter), Facebook, and more.  
- **Easy UI management** – intuitive interface to control streaming and posting.  

*(Add more specific features here, e.g., scheduling, multi-streaming, or analytics support.)*

---

## Installation

1. Clone the repository:
    ```bash
    git clone https://github.com/GodWasAProgrammer/SSMM_UI.git
    cd SSMM_UI
    ```

2. Open the solution in your IDE (e.g., Visual Studio).

3. Build the project:
    - **Visual Studio**: Use the `.sln` file (`SSMM_UI.sln`).  
    - **dotnet CLI**:  
      ```bash
      dotnet build
      ```

4. Download : https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full-shared.7z <---- required
copy the contents of the extracted "bin" dir into "dependencies".

5. Run the application:
    ```bash
    dotnet run --project SSMM_UI
    ```
  
---

## Usage

1. Start the application.  

3. Connect your streaming client (OBS, or equivalent).
  
4. Select the target platform(s) and posting method. 

**Not implemented fully yet**
 
5. Start streaming – the app will handle the rest.  

*(Add screenshots, step-by-step UI flows, and examples here.)*

---

## Configuration

Coming Soon 