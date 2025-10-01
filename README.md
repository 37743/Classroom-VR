<div align="center">

[![LATEST](https://img.shields.io/badge/AI_Accelerated_Appathon:_From_Concept_to_Code_with_Generative_AI-ffd8ff?style=for-the-badge&label=Appathon&l&labelColor=3b0d3d)](https://www.facebook.com/ejust.official/posts/-ai-accelerated-appathon-from-concept-to-code-with-generative-aihosted-by-the-co/749010944187251/)

<a>
  <p align="center">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="Images\Webpage\banner-3.png">
      <img height="200px" src="Images\Webpage\banner-3.png">
    </picture>
  </p>
</a>

</div>

---

# Classroom VR: A project by Team 10Q <img src="Images/Webpage/happy-mr-rashed.png" width="32">

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
- Download the latest APK release from the [Releases](https://github.com/37743/Classroom-VR/releases) sidepanel. 
- On SideQuest, select Install APK from folder (down-arrow icon) → pick your .apk → wait for "Success".
- Now you can run the application locally through: Apps → filter Unknown Sources → launch your app.

### Development Build (optional)

- Clone the repository (with Git LFS).
- Import the project with the exact Unity version from ProjectSettings/ProjectVersion.txt (via Unity Hub).
  - Unity packages/dependencies are included in Packages/packages.json
  - RAG servers would require you to install the packages in each respective directory's ~/requirements.txt
- Configure your build with the following: Android / Oculus XR / ARM64 / IL2CPP.
- Launch the build using Playmode.

***DISCLAIMER:***

- You would need to host and connect (through the inspector) your own RAG/LLM/SQL servers on your machine using the models (optional) uploaded in ~/RAG Models/ and database backup uploaded in ~/SQL/ as well as any API keys.
  - **EXAMPLE:**
    - 1. Set up your curriculum data files: `Bio_curriculum_chunks1000_over20.csv`, `Bio_curriculum_faiss_index_1000_over20.bin`
      2. Configure server settings:
         ```python
         API_KEY = "your_groq_api_key"
         HOST = "your_server_ip"
         PORT = 8000
         ```
      3. Start the server:
         ```bash
         python mr_rashid_server.py
         ```
         
## Usage

1. Login with your account.

<div align="center">

|  |
| :-----------------------------------------------: |
|              VR Classroom Login Menu              |

</div>

2. Choose your VR classroom and AI tutor.

<div align="center">

|  |
| :--------------------------------------------------: |
|                VR Classroom Selection                |

</div>

3. Connect to your chosen virtual learning environment.

<div align="center">

|  |
| :--------------------------------------------: |
|       Immersive VR Learning Environment       |

</div>

4. Interact with your AI tutor (Mr. Rashed shown).

<div align="center">

|  |
| :-----------------------------------------------------------------: |
|                    Interactive AI Tutor Session                    |

</div>

5. Get personalized explanations and curriculum content.

<div align="center">

|  |
| :---------------------------------------------: |
|        Personalized Learning Experience        |

</div>

6. Practice with generated questions and exam preparation.

<div align="center">

|  |
| :----------------------------------------: |
|       Practice Questions & Exam Prep       |

</div>

7. Explore Quiz mode and challenge yourself.

<div align="center">

|  |
| :-----------------------------------------: |
|                  Quiz mode                  |

</div>

## Frequently Asked Questions (FAQ)

- What makes Classroom VR teachers different from other AI tutors?
  - **ANSWER:** *Classroom VR teachers, for example Mr. Rashed, are specifically designed for their respective curriculum (e.g. Egyptian senior highschool year biology) with RAG-powered accuracy. Unlike generic AI models, it provides customized responses, remembers your learning context, and automatically detects what type of help you need (explanations, summaries, exam prep, etc.).*

## Application Stack
- Built in Unity 6 with Universal Render Pipeline (URP)
- Android Platform (ARMx64)
- Unity Sentis (also known as Unity Inference Engine) for execution of AI models
- Meta XR SDK (previously known as Oculus Integration) for VR framework
- SQL Database as a medium for storing users/teachers information, the schema shown below:

<div align="center">

| <img width="512" height="512" alt="vr_teacher Schema" src="Images/SQL_Schema.png" /> |
| :----------------------------------------: |
|       vr_teacher Database ER Diagram     |

</div>
  
## Generative AI Usage Log
### Speech-to-Text & Text-to-Speech
- Open AI's open-source [Whisper Tiny](https://huggingface.co/openai/whisper-tiny) for automatic speech recognition. (supports English/German/French)
  - A state machine manages and runs the spectrogram model, encoder model, and decoder model.

<div align="center">

| <img width="512" height="512" alt="Whisper Architecture" src="Images/Webpage/Whisper-Tiny-architecture.png" /> |
| :----------------------------------------: |
|       Whisper Tiny Architecture [(Source)](https://www.researchgate.net/figure/Whisper-Tiny-architecture_fig5_391777216)     |

</div>

- [Piper](https://github.com/OHF-Voice/piper1-gpl) (Piper phonemizer + eSpeak NG text-to-speech synthesizer) for free-software neural text-to-speech.It is made using VITS: Conditional Variational Autoencoder with Adversarial Learning for End-to-End Text-to-Speech.

<div align="center">

| <img width="512" height="512" alt="VITS Architecture" src="Images/Webpage/VITS-architecture.png" /> |
| :----------------------------------------: |
|       VITS Pipeline [(Source)](https://arxiv.org/abs/2106.06103)      |

</div>

- Generated audio is processed and in turn generates visemes which are used for OVR Lip Sync with teacher model's facial blendshapes.

<div align="center">
  
| ![hippo](https://scontent.fcai19-3.fna.fbcdn.net/v/t39.2365-6/64637900_622042114964562_89558726775668736_n.gif?_nc_cat=105&ccb=1-7&_nc_sid=e280be&_nc_ohc=N_enESG_aZ0Q7kNvwGRxTMP&_nc_oc=AdnOHaI6rDWte57AY218ZXxyGMZMX70xSMfkbliusTzNjDZIax6I9uqGq4B4mbFyCHU&_nc_zt=14&_nc_ht=scontent.fcai19-3.fna&_nc_gid=WdwVN9niYrUeS1SsvWBOWw&oh=00_Afbfim6ua9NDNfopm63dpdyV_y2fGPqOWCmDbKjjcJhoFQ&oe=68F72F40) |
| :----------------------------------------: |
|       Animated Lipsync Example [(Source)]([https://arxiv.org/abs/2106.06103](https://developers.meta.com/horizon/documentation/unity/audio-ovrlipsync-unity/))     |

</div>

### Prompts:
- There are 2 types of prompts generated and used in this project, both of which use TCP/IP communication with external servers hosting RAG models, and each accommodate for a specific mode:
  - **Free Questions Mode:** In this mode, you are free to speak to the chosen teacher, asking them to explain, summarize, generate mindmap/questions in relation to the assigned curriculum. The model blocks the user attempts when asking for anything out of context.
    - Prompt & Response Examples: (JSON format)
      
<div align="center">
  
| Prompt |
| :----------------------------------------: |
| |
| Response |
| |

</div>

  - **Quiz Mode:** While in this mode, which you are only able to join during specific times (queried through SQL database), the models generate 10 questions, which are answered via STT, the results are then stored in the SQL database.
    - Prompt & Response Examples: (JSON format)

<div align="center">
  
| Prompt |
| :----------------------------------------: |
| |
| Response |
| |

</div>

### Additional Usages of Gen AI
- RAG models use Sentence Transformers for semantic embedding generation, FAISS for similarity search and clustering and GROQ API (temporarily) for high-performance inference.
- Most models and textures are made firsthand using Blender, while some textures (like the logo, and decals) are generated via [Stable Diffusion](https://github.com/Stability-AI/stablediffusion).

## Credits

### Development Team & Contributors

- [@MarwanZaineldeen](https://github.com/MarwanZaineldeen) - **Marwan Tamer Hanafy Zaineldeen** - Project Lead & AI Engineering
- [@37743](https://github.com/37743) - **Yousef Ibrahim Gomaa Mahmoud** - Unity Development & AI-VR Integration
- [@MaiYasser03](https://github.com/MaiYasser03) - **Mai Yasser Ouf** - NLP Expert & Database System Administration
