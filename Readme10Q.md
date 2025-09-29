<div align="center">

[NOTICE]: <> (Use "#region folding" extension by maptz for a better experience reading this file)

[![LATEST](https://)](https://)
[![WEBSITE]()


<a>
  <p align="center">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="images/Logo">
      <img height="200px" src="images/mr_rashid_logo.png">
    </picture>
  </p>
</a>

</div>

***

# 10Q - Immersive VR Educational Classroom

An advanced VR education classroom featuring specialized AI tutors for different subjects. Experience personalized learning through immersive virtual reality with curriculum-aligned AI teachers who adapt to your learning style.

## Features:
- Multiple specialized AI tutors including Mr. Rashid (Biology), Dr. Enas (Chemistry), Prof. Ahmed (Physics)!
- Choose your VR classroom and subject-specific learning experience!
- RAG-powered curriculum accuracy for official secondary education standards!
- Multi-intent detection: Q&A, explanations, summaries, exam prep, and more!
- VR-optimized responses perfectly crafted for whiteboard display and text-to-speech!
- Contextual memory that remembers your learning journey across sessions!
- Quiz Mode where you can follow up your quizzes and test your knowledge and have the result immediately!
- TCP socket server supporting multi-turn conversations and real-time interaction!


## Available VR Classrooms:

### Mr. Rashid - Biology Expert
- Official 3rd secondary biology curriculum mastery
- Specializes in cellular biology, genetics, human systems, and ecology
- Patient mentor who makes complex biological concepts accessible

### Dr. Enas - Chemistry Specialist (Coming Soon)
- Advanced chemistry curriculum coverage
- Interactive molecular modeling and reaction explanations
- Laboratory safety and experiment guidance

### Prof. Ahmed - Physics Master (Coming Soon)
- Comprehensive physics education from mechanics to quantum
- Visual demonstrations of complex physical phenomena
- Problem-solving strategies and mathematical applications

## Usage

1. Register an account.
   
<div align="center">

| ![Classroom selection screenshot](screenshots/Registeration.png")|
|:---:|
| VR Classroom Registeration |

</div>

2. Choose your VR classroom and AI tutor.
   
<div align="center">

| ![Classroom selection screenshot](screenshots/)|
|:---:|
| VR Classroom Selection |

</div>

3. Connect to your chosen virtual learning environment.

<div align="center">

| ![VR environment screenshot](screenshots/vr_env.webp)|
|:---:|
| Immersive VR Learning Environment |

</div>

4. Interact with your AI tutor (Mr. Rashid shown).

<div align="center">

| ![AI tutor interaction screenshot](screenshots/Tutor_interaction.jpg)|
|:---:|
| Interactive AI Tutor Session |

</div>

5. Get personalized explanations and curriculum content.

<div align="center">

| ![Personalized learning screenshot](screenshots/)|
|:---:|
| Personalized Learning Experience |

</div>

6. Practice with generated questions and exam preparation.

<div align="center">

| ![Practice session screenshot](screenshots/)|
|:---:|
| Practice Questions & Exam Prep |

</div>

7. Explore Quiz mode and challenge yourself.

<div align="center">

| ![Learning progress screenshot](screenshots/)|
|:---:|
| Quiz mode |

</div>

## Frequently Asked Questions (FAQ)

- What makes Mr. Rashed different from other AI tutors?
  - **ANSWER:** *Mr. Rashed is specifically designed for the official 3rd secondary biology curriculum with RAG-powered accuracy. Unlike generic AI models, it provides VR-optimized responses, remembers your learning context, and automatically detects what type of help you need (explanations, summaries, exam prep, etc.).*
- How does the VR optimization work?
  - **ANSWER:** *Responses are crafted to be 70-120 words for perfect VR attention spans, with no formatting symbols that could break your VR interface, natural speech patterns for text-to-speech, and content that displays beautifully on VR whiteboards.*
- Can Mr. Rashid remember previous conversations?
  - **ANSWER:** *Yes, the system maintains conversation history and builds connected learning experiences across sessions, allowing for meaningful multi-turn discussions and progressive learning.*
- What types of help can I get?
  - **ANSWER:** *Mr. Rashed automatically detects your intent and provides: detailed explanations, concise summaries, practice questions, exam preparation tips, concept mapping, and comprehensive Q&A support.*

## Technical Architecture

### RAG System Components:
- **FAISS Index**: Vector database for semantic search across curriculum content
- **Sentence Transformers**: Multilingual E5-base model for embedding generation  
- **Curriculum Chunks**: Processed biology curriculum content with metadata
- **Intent Detection**: Automatic recognition of student learning objectives

### Server Infrastructure:
- **TCP Socket Server**: Multi-threaded architecture supporting concurrent VR clients
- **JSON Communication**: Structured message format with timestamps for performance tracking
- **Conversation Management**: Persistent history storage and context-aware responses
- **Groq Integration**: Powered by Kimi-K2-Instruct model for educational content generation

## Installation & Setup

### Requirements:
```bash
pip install groq sentence-transformers faiss-cpu pandas numpy
```

### Configuration:
1. Set up your curriculum data files:
   - `Bio_curriculum_chunks1000_over20.csv`
   - `Bio_curriculum_faiss_index_1000_over20.bin`

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

## Credits
### Development Team & Contributors

- [@](https://github.com/) - **Name1** - Project Lead & AI Engineering
- [@](https://github.com/) - **Name2** - VR Integration
- [@](https://github.com/) - **Name3** - System Architecture

### Technology Stack & Libraries
- **Groq API**, for high-performance language model inference,
- **Sentence Transformers**, for semantic embedding generation,
- **FAISS**, for efficient similarity search and clustering,
- **Pandas**, for curriculum data processing and management,
- **NumPy**, for numerical computations and array operations,
- **Socket Programming**, for real-time VR client communication,
- **Threading**, for concurrent multi-client support.

---