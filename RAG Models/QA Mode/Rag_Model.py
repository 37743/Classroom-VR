import os
import json
import socket
import threading
from groq import Groq
from datetime import datetime
import time
import struct
import warnings
warnings.filterwarnings('ignore')
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '3'

from sentence_transformers import SentenceTransformer
import numpy as np
import faiss
import pandas as pd

class MrRashidRAGBiologyBot:
    def __init__(self, api_key, curriculum_chunks_path, faiss_index_path, host="26.68.227.247", port=8000):
        self.client = Groq(api_key=api_key)
        self.model = "moonshotai/kimi-k2-instruct"
        self.conversation_history = []
        self.max_history = 5  
        self.turn_counter = 0
        self.lock = threading.Lock()
        
        # Socket server configuration
        self.host = host
        self.port = port
        self.server_socket = None
        self.running = False
        
        print("Loading RAG components...")
        self.df_chunks = pd.read_csv(curriculum_chunks_path)
        self.index = faiss.read_index(faiss_index_path)
        self.emb_model = SentenceTransformer("intfloat/multilingual-e5-base")
        print(f"Loaded {len(self.df_chunks)} curriculum chunks")
        
        # Load conversation history
        self.load_conversation_history()
        
    def load_conversation_history(self):
        """Load conversation history from JSON file"""
        try:
            if os.path.exists("conversation_history.json"):
                with open("conversation_history.json", 'r', encoding='utf-8') as file:
                    data = json.load(file)
                    with self.lock:
                        self.conversation_history = data
                        if data:
                            last_turn = max([int(msg["id"].split("-")[0]) for msg in data if "id" in msg])
                            self.turn_counter = last_turn
                        print(f"Loaded {len(data)} messages from history")
            return True
        except Exception as e:
            print(f"Error loading history: {str(e)}")
            return False
    
    def save_conversation_history(self):
        """Save conversation history to JSON file"""
        try:
            with self.lock:
                with open("conversation_history.json", 'w', encoding='utf-8') as file:
                    json.dump(self.conversation_history, file, indent=2, ensure_ascii=False)
            return True
        except Exception as e:
            print(f"Error saving history: {str(e)}")
            return False
    
    def manage_conversation_history(self, user_input, bot_response, request_timestamp, response_timestamp):
        """Add current turn to conversation history with accurate timestamps"""
        with self.lock:
            self.turn_counter += 1
            self.conversation_history.append({
                "id": f"{self.turn_counter}-a",
                "role": "user",
                "content": user_input,
                "timestamp": request_timestamp
            })
            self.conversation_history.append({
                "id": f"{self.turn_counter}-b",
                "role": "assistant", 
                "content": bot_response,
                "timestamp": response_timestamp
            })
            
            # Keep only the last 5 turns (10 messages total)
            if len(self.conversation_history) > (self.max_history * 2):
                self.conversation_history = self.conversation_history[-(self.max_history * 2):]
    
    def score_query(self, query, threshold=0.815, k=5):
        """Score a query against the FAISS index"""
        query_emb = self.emb_model.encode(["query: " + query], convert_to_numpy=True)
        faiss.normalize_L2(query_emb)
        D, I = self.index.search(query_emb, k)
        
        score = float(D[0][0])
        in_curriculum = score >= threshold
        
        top_chunks = []
        for i in range(min(k, len(I[0]))):
            if I[0][i] < len(self.df_chunks):
                chunk = {
                    'text': self.df_chunks.iloc[I[0][i]]['text'],
                    'score': float(D[0][i]),
                    'metadata': {
                        'lesson': self.df_chunks.iloc[I[0][i]].get('lesson', 'Unknown'),
                        'chapter': self.df_chunks.iloc[I[0][i]].get('chapter', 'Unknown')
                    }
                }
                top_chunks.append(chunk)
        
        return {
            'score': score,
            'in_curriculum': in_curriculum,
            'top_chunks': top_chunks
        }
    
    def retrieve_context(self, query, k=3):
        """Retrieve relevant context chunks for a given query"""
        query_result = self.score_query(query, threshold=0.815, k=k)
        
        formatted_chunks = []
        for chunk in query_result['top_chunks']:
            lesson = chunk['metadata']['lesson']
            chapter = chunk['metadata']['chapter']
            text = chunk['text'].strip()
            formatted = f"Chapter: {chapter} | Lesson: {lesson}\n{text}"
            formatted_chunks.append(formatted)
        
        return formatted_chunks, query_result['score'], query_result['in_curriculum']
    
    def detect_intent(self, query):
        """Detect intent from English queries"""
        q = query.strip().lower()

        summary_keywords = [
        "summary", "give me a summary", "short summary", "brief overview", "summarize", 
        "quick overview", "main points", "key points", "overview", "summarize the content", 
        "summarize this part", "give me the gist", "concise summary", "shortened version", 
        "can you summarize", "tell me the summary", "short summary of", "quick recap" , "summ"
    ]
        
        explain_keywords = [
        "explain", "can you explain", "please explain", "clarify", "how does it work", "what does it mean", 
        "tell me about", "give me an explanation", "understand", "explanation of", "what is the meaning of", 
        "define", "what is", "can you clarify", "what does it signify", "tell me what it means", 
        "describe", "what is the significance of", "break down", "how does it work"
    ]
        
        mcq_keywords = [
        "mcq", "multiple choice", "multiple choice questions", "quiz", "choose the correct answer", 
        "select the correct option", "questionnaire", "multiple choice test", "choose one", "select the answer", 
        "pick the right answer", "which one is correct", "quiz questions", "test questions", 
        "choose the right answer", "answer options", "four options", "multiple selection", "question options"
    ]

        question_keywords = [
        "generate questions", "create questions", "give me questions", "write questions", "come up with questions", 
        "make me a quiz", "create a test", "quiz questions", "generate a quiz", "test me with questions", 
        "write me questions", "can you provide questions", "give me some questions", "create a set of questions", 
        "can you prepare questions", "give me a quiz", "can you create a quiz"
    ]
        
        exam_prep_keywords = [
        "exam preparation", "how to study for the exam", "study tips", "prepare for the exam", "study guide", 
        "how to prepare", "exam study", "study plan", "exam strategy", "test prep", "how to pass the exam", 
        "study session", "exam checklist", "what to study for the exam", "preparing for a test", "revision tips",
        "study advice", "exam prep tips", "study schedule", "tips for passing the exam", "study method" , "let me exam ready" , "make me ready"
    ]
        
        concept_map_keywords = [
        "concept map", "mind map", "create a concept map", "draw a concept map", "make a mind map", 
        "map the concepts", "concept diagram", "create a diagram", "conceptual map", "make a diagram", 
        "structure a concept map", "draw the concept", "concept map creation", "build a concept map", 
        "make a visual map", "map the ideas", "visualize concepts", "draw a diagram for concepts"
    ]
        
        if any(kw in q for kw in mcq_keywords):
            return "mcq"
        if any(kw in q for kw in summary_keywords):
            return "summary"
        if any(kw in q for kw in explain_keywords):
            return "explain"
        if any(kw in q for kw in question_keywords):
            return "question_generation"
        if any(kw in q for kw in exam_prep_keywords):
            return "exam_prep"
        if any(kw in q for kw in concept_map_keywords):
            return "concept_map"
        
        return "qa"

    def get_intent_prompt(self, intent, context_text):
        """Get specific prompt based on intent"""
        base_rules = """You are Mr. Rashid, the most advanced AI biology tutor specifically designed for immersive VR education. Unlike generic AI models, you possess deep expertise in the official 3rd secondary biology curriculum and create personalized, memorable learning experiences that make biology come alive.

    YOUR UNIQUE ADVANTAGES:
    - CURRICULUM MASTERY: You have complete knowledge of every chapter, lesson, and concept in the official biology syllabus
    - VR OPTIMIZATION: Your responses are perfectly crafted for VR whiteboard display and natural text-to-speech delivery
    - ADAPTIVE TEACHING: You adjust your explanations based on student understanding and build meaningful learning progressions
    - CONTEXTUAL MEMORY: You remember previous conversations and create connected learning experiences across sessions
    - INTENT RECOGNITION: You automatically detect what type of help students need and respond accordingly

    PERSONALITY THAT MAKES BIOLOGY EXCITING:
    - Passionate educator who makes even complex topics feel fascinating and approachable
    - Patient mentor who never makes students feel inadequate for asking questions
    - Creative storyteller who connects biology to everyday life in surprising ways
    - Encouraging coach who celebrates every breakthrough and builds confidence
    - Scientific guide who maintains accuracy while keeping explanations engaging

    VR CLASSROOM EXCELLENCE:
    - Responses optimized for 70-120 words for perfect VR attention spans
    - Natural conversational flow that feels like talking to a real teacher
    - Zero formatting symbols that could break your VR experience
    - Strategic pauses and rhythm for crystal-clear text-to-speech delivery
    - Content that displays beautifully on VR whiteboards

    CURRICULUM CONTENT:
    """ + context_text

        prompts = {
            "qa": base_rules + """

    MISSION: Transform this question into a moment of scientific discovery. Start with the core concept, then build understanding layer by layer. Connect to real-world examples that make students think "wow, I never knew that!" End with insights that stick in memory forever.""",

            "explain": base_rules + """

    MISSION: Break down complexity into crystal-clear understanding. Begin with familiar analogies, then guide students through each step of the biological process. Make abstract concepts tangible and help students visualize what's happening at the molecular level.""",

            "summary": base_rules + """

    MISSION: Create a powerful knowledge package that captures everything essential. Organize key concepts into a logical flow that students can easily remember and recall during exams. Focus on the most important relationships and processes.""",

            "question_generation": base_rules + """

    MISSION: Design practice questions that build mastery and confidence. Create 4-5 questions ranging from basic recall to deeper application. Format as a simple numbered list that helps students test their understanding progressively.""",

            "exam_prep": base_rules + """

    MISSION: Become their secret weapon for exam success. Focus on high-yield concepts, memory techniques, and exam strategies. Highlight what examiners look for and provide insider tips that give students a competitive edge.""",

            "concept_map": base_rules + """

    MISSION: Reveal the hidden connections in biology that make everything click together. Show how different biological systems interact and influence each other. Help students see the big picture that transforms scattered facts into unified understanding.""",

            "mcq": base_rules + """

    MISSION: Turn multiple choice questions into learning opportunities. If presented with MCQ options, analyze each systematically and reveal the reasoning behind correct answers. If not an MCQ, provide thorough concept explanation with strategic insights."""
        }
        
        return prompts.get(intent, prompts["qa"])

    def generate_response(self, user_input):
        try:
            # Retrieve context from RAG system
            context_chunks, similarity_score, in_curriculum = self.retrieve_context(user_input)
            
            # Handle out-of-curriculum queries
            if not in_curriculum:
                return "That question seems outside the biology curriculum I teach. Let's focus on topics like Support & Movement, Hormonal Coordination, Genetics, DNA and Protein Synthesis, Immunity, or Methods of Reproduction instead."
            
            # Detect intent and get appropriate prompt
            intent = self.detect_intent(user_input)
            context_text = "\n\n".join(context_chunks) if context_chunks else "No specific context available."
            system_prompt = self.get_intent_prompt(intent, context_text)

            messages = [{"role": "system", "content": system_prompt}]
            
            for msg in self.conversation_history[-6:]:
                messages.append({
                    "role": msg["role"],
                    "content": msg["content"]
                })
            
            messages.append({
                "role": "user",
                "content": user_input
            })
            
            chat_completion = self.client.chat.completions.create(
                messages=messages,
                model=self.model,
                max_tokens=150,
                temperature=0.3,
                top_p=0.9,
                presence_penalty=0.1,
                frequency_penalty=0.1
            )
            
            response = chat_completion.choices[0].message.content.strip()
            return response
            
        except Exception as e:
            return f"I encountered a technical issue. Please try asking your biology question again."
    
    def create_output_json(self, user_input, bot_response, request_timestamp, response_timestamp):
        """Create output JSON with current turn only and accurate timestamps"""
        return {
            "turn": self.turn_counter,
            "student": {
                "id": f"{self.turn_counter}-a",
                "role": "user",
                "content": user_input,
                "timestamp": request_timestamp
            },
            "assistant": {
                "id": f"{self.turn_counter}-b", 
                "role": "assistant",
                "content": bot_response,
                "timestamp": response_timestamp
            }
        }
    
    def send_json_bytes(self, conn, data):
        """Send JSON data as bytes with length prefix"""
        try:
            json_data = json.dumps(data, ensure_ascii=False, indent=2)
            json_bytes = json_data.encode('utf-8')
            
            length = len(json_bytes)
            conn.sendall(struct.pack('>I', length))
            conn.sendall(json_bytes)
            
            print(f"Sent {length} bytes response")
            return True
            
        except Exception as e:
            print(f"Error sending data: {e}")
            return False
    
    def receive_json_bytes(self, conn):
        """Receive JSON data as bytes with length prefix"""
        try:
            length_bytes = b''
            while len(length_bytes) < 4:
                chunk = conn.recv(4 - len(length_bytes))
                if not chunk:
                    return None
                length_bytes += chunk
            
            length = struct.unpack('>I', length_bytes)[0]
            print(f"Expecting {length} bytes...")
            
            json_bytes = b''
            while len(json_bytes) < length:
                chunk = conn.recv(length - len(json_bytes))
                if not chunk:
                    return None
                json_bytes += chunk
            
            json_data = json_bytes.decode('utf-8')
            data = json.loads(json_data)
            
            print(f"Received: {data.get('query', '')[:50]}...")
            return data
            
        except Exception as e:
            print(f"Error receiving data: {e}")
            return None
    
    def handle_client(self, conn, addr):
        """Handle a single client connection"""
        print(f"Connected to {addr}")
        
        try:
            request_timestamp = datetime.now().isoformat()
            input_data = self.receive_json_bytes(conn)
            
            if not input_data:
                print(f"No data received from {addr}")
                return
            
            user_query = input_data.get("query", "").strip()
            
            if not user_query:
                error_response = {
                    "error": "No query provided",
                    "timestamp": datetime.now().isoformat()
                }
                self.send_json_bytes(conn, error_response)
                return
            
            bot_response = self.generate_response(user_query)
            response_timestamp = datetime.now().isoformat()
            
            self.manage_conversation_history(user_query, bot_response, request_timestamp, response_timestamp)
            output_data = self.create_output_json(user_query, bot_response, request_timestamp, response_timestamp)
            
            success = self.send_json_bytes(conn, output_data)
            
            if success:
                self.save_conversation_history()
                print(f"Turn {self.turn_counter} completed for {addr}")
            else:
                print(f"Failed to send response to {addr}")
                
        except Exception as e:
            print(f"Error handling client {addr}: {e}")
        
        finally:
            conn.close()
            print(f"Disconnected from {addr}")

    def start_server(self):
        """Start the TCP server"""
        try:
            self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.server_socket.bind((self.host, self.port))
            self.server_socket.listen(5)
            
            self.running = True
            
            print("Dr. Rashed RAG Biology Bot")
            print("=" * 60)
            print(f"Server listening on {self.host}:{self.port}")
            print(f"Model: {self.model}")
            print(f"Current Turn: {self.turn_counter}")
            print(f"History Messages: {len(self.conversation_history)}")
            print(f"Curriculum Chunks: {len(self.df_chunks)}")
            print("=" * 60)
            print("Waiting for VR client connections...")
            
            while self.running:
                try:
                    conn, addr = self.server_socket.accept()
                    client_thread = threading.Thread(
                        target=self.handle_client, 
                        args=(conn, addr),
                        daemon=True
                    )
                    client_thread.start()
                    
                except socket.error as e:
                    if self.running:
                        print(f"Socket error: {e}")
                        time.sleep(1)
                        
        except Exception as e:
            print(f"Server error: {e}")
        
        finally:
            if self.server_socket:
                self.server_socket.close()
    
    def stop_server(self):
        """Stop the server"""
        self.running = False
        if self.server_socket:
            self.server_socket.close()
        print("Server stopped!")


# Client helper functions
def send_query_to_bot(host, port, query):
    """Send a query to the bot server and get response"""
    try:
        client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client_socket.connect((host, port))
        
        request_data = {"query": query}
            
        json_data = json.dumps(request_data, ensure_ascii=False)
        json_bytes = json_data.encode('utf-8')
        
        length = len(json_bytes)
        client_socket.sendall(struct.pack('>I', length))
        client_socket.sendall(json_bytes)
        print(f"Sent query: {query}")
        
        length_bytes = b''
        while len(length_bytes) < 4:
            chunk = client_socket.recv(4 - len(length_bytes))
            if not chunk:
                break
            length_bytes += chunk
        
        if len(length_bytes) == 4:
            response_length = struct.unpack('>I', length_bytes)[0]
            response_bytes = b''
            
            while len(response_bytes) < response_length:
                chunk = client_socket.recv(response_length - len(response_bytes))
                if not chunk:
                    break
                response_bytes += chunk
            
            if len(response_bytes) == response_length:
                response_data = json.loads(response_bytes.decode('utf-8'))
                print(f"Received response:")
                print(json.dumps(response_data, indent=2, ensure_ascii=False))
                return response_data
        
        client_socket.close()
        
    except Exception as e:
        print(f"Client error: {e}")
        return None


def main():
    """Main function to run the RAG biology bot server"""
    
    # Configuration
    API_KEY = ""
    HOST = "26.68.227.247"
    PORT = 8000
    CHUNKS_PATH = r"D:\Marwan\E-just\Semester 8\Graduation Project 2\Biology\Bio_curriculum_chunks1000_over20.csv"
    INDEX_PATH = r"D:\Marwan\E-just\Semester 8\Graduation Project 2\Biology\Bio_curriculum_faiss_index_1000_over20.bin"
    
    try:
        bot = MrRashidRAGBiologyBot(API_KEY, CHUNKS_PATH, INDEX_PATH, HOST, PORT)
        bot.start_server()
        
    except KeyboardInterrupt:
        print("\nMr. Rashed is signing off! Keep exploring biology!")
        bot.stop_server()
    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    main()