<div align="center">

[![LATEST](https://img.shields.io/badge/AI_Accelerated_Appathon:_From_Concept_to_Code_with_Generative_AI-ffd8ff?style=for-the-badge&label=Appathon&l&labelColor=3b0d3d)](https://www.facebook.com/ejust.official/posts/-ai-accelerated-appathon-from-concept-to-code-with-generative-aihosted-by-the-co/749010944187251/)

<a>
  <p align="center">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="Images/Webpage">
      <img height="200px" src="Images\Webpage\banner-3.png">
    </picture>
  </p>
</a>

</div>

---

# Classroom VR: A project by Team 10Q

An advanced VR education classroom featuring specialized AI tutors for different subjects. Experience personalized learning through immersive virtual reality with curriculum-aligned AI teachers who adapt to your learning style.

## Demo Video:

## Features: (as of now)

- Specialized AI teacher in an Interactive VR environment: Mr. Rashid (Biology)!
- RAG-powered curriculum accuracy for official secondary education standards!
- Multi-intent detection: Q&A, explanations, summaries, exam prep, and more!
- Optimized responses perfectly crafted for display and text-to-speech!
- Contextual memory that remembers your learning journey across sessions!
- Quiz Mode where you can follow up your quizzes and test your knowledge and have the result immediately!

### Available VR Classrooms:

#### - Mr. Rashed - Biology Expert (Our Mascot)

- Egyptian secondary senior biology curriculum mastery.
- Specializes in cellular biology, genetics, human systems, and ecology.
- Patient mentor who makes complex biological concepts accessible.
- Available in **Free Questions** mode and **Quiz** mode!

#### NOTE:

- In the long term, we plan to include Ms. Inas, a Chemistry Specialist and Mr. Sheriff, a Physics Master as part of our program
- You could also register as a teacher with us! Contact: [yousef.gomaa@ejust.edu.eg](mailto:yousef.gomaa@ejust.edu.eg)

## Hardware Requirements:

- Meta Quest 2/3/3S with ≥1 GB free storage.
- A good USB-C cable (USB 3.0 preferred) or Air Link (optional). (for development build)

## Setup Method:

### APK File

- Install the Meta Quest app on your phone and log in with your Meta account.
- Pair your headset in the app.
- In the phone app: Menu → Devices → your Quest → Developer Mode → On.
- Reboot the headset, then enable "Allow USB Debugging" when connecting the cable with your preferred device of choice.
- Install SideQuest (desktop).
- Plug in the Quest, ensure the top-left dot in SideQuest is green (authorized).
- Download the latest APK release from the Releases sidepanel.
- On SideQuest, select Install APK from folder (down-arrow icon) → pick your .apk → wait for "Success".
- Now you can run the application locally through: Apps → filter Unknown Sources → launch your app.

### Development Build (optional)

- Clone the repository (with Git LFS).
- Import the project with the exact Unity version from ProjectSettings/ProjectVersion.txt (via Unity Hub).
- - Unity packages/dependencies are included in Packages/packages.json
- Configure your build with the following: Android / Oculus XR / ARM64 / IL2CPP.
- Launch the build using Playmode.

***DISCLAIMER:***

- You would need to host and connect (through the inspector) your own RAG/LLM/SQL servers on your machine using the models (optional) uploaded in ~/RAG Models/ and ~/SQL/ as well as any API keys, more information of how we have done it will be explained below.

## Usage

1. Login with your account.

<div align="center">

| ![Classroom selection screenshot](Images/Login.png) |
| :-----------------------------------------------: |
|              VR Classroom Login Menu              |

</div>

2. Choose your VR classroom and AI tutor.

<div align="center">

| ![Classroom selection screenshot](Images/Teachers.png) |
| :--------------------------------------------------: |
|                VR Classroom Selection                |

</div>

3. Connect to your chosen virtual learning environment.

<div align="center">

| ![VR environment screenshot](Images/vr_env.webp) |
| :--------------------------------------------: |
|       Immersive VR Learning Environment       |

</div>

4. Interact with your AI tutor (Mr. Rashed shown).

<div align="center">

| ![AI tutor interaction screenshot](screenshots/Tutor_interaction.jpg) |
| :-----------------------------------------------------------------: |
|                    Interactive AI Tutor Session                    |

</div>

5. Get personalized explanations and curriculum content.

<div align="center">

| ![Personalized learning screenshot](screenshots/) |
| :---------------------------------------------: |
|        Personalized Learning Experience        |

</div>

6. Practice with generated questions and exam preparation.

<div align="center">

| ![Practice session screenshot](screenshots/) |
| :----------------------------------------: |
|       Practice Questions & Exam Prep       |

</div>

7. Explore Quiz mode and challenge yourself.

<div align="center">

| ![Learning progress screenshot](screenshots/) |
| :-----------------------------------------: |
|                  Quiz mode                  |

</div>

## Generative AI Usage Log

## Frequently Asked Questions (FAQ)

- What makes Classroom VR teachers different from other AI tutors?
  - **ANSWER:** *Classroom VR teachers, for example Mr. Rashed, are specifically designed for their respective curriculum (e.g. Egyptian senior highschool year biology) with RAG-powered accuracy. Unlike generic AI models, it provides customized responses, remembers your learning context, and automatically detects what type of help you need (explanations, summaries, exam prep, etc.).*

## Technical Architecture:

### RAG

### RAG System Components:

- **FAISS Index**: Vector database for semantic search across curriculum content
- **Sentence Transformers**: Multilingual E5-base model for embedding generation
- **Curriculum Chunks**: Processed biology curriculum content with metadata
- **Intent Detection**: Automatic recognition of student learning objectives
- **Conversation Management**: Persistent history storage and context-aware responses

### Server Infrastructure:

- **TCP Socket Server**: Multi-threaded architecture supporting concurrent VR clients
- **JSON Communication**: Structured message format with timestamps for auditing

## Credits

### Development Team & Contributors

- [@MarwanZaineldeen](https://github.com/MarwanZaineldeen) - **Marwan Tamer Hanafy Zaineldeen** - Project Lead & AI Engineering
- [@37743](https://github.com/37743) - **Yousef Ibrahim Gomaa Mahmoud** - Unity Development & AI-VR Integration
- [@MaiYasser03](https://github.com/MaiYasser03) - **Mai Yasser Ouf** - NLP Expert & Database System Administration

---
