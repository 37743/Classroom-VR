"""
code to generate Quizzes in the quiz classroom
-add your grok api key to use the llm
- Update HOST to the desired interface or IP.
- Update PORT to the desired port number.
"""

import os
import json
import socket
import struct
import requests
from sentence_transformers import SentenceTransformer
import faiss
import numpy as np

HOST = "26.235.96.91"
PORT = 8000
DATASET_JSON = "bio_final_cleaned.json"
EMBEDDING_MODEL_NAME = "all-MiniLM-L6-v2"
TOP_K = 1

GROQ_API_KEY = os.environ.get("GROQ_API_KEY")
GROQ_CHAT_ENDPOINT = "https://api.groq.com/openai/v1/chat/completions"
GROQ_MODEL = "llama-3.1-8b-instant"

def load_dataset(json_path):
    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    texts = []
    for chapter in data.get("chapters", []):
        chap_title = chapter.get("chapter_title", "")
        for lesson in chapter.get("lessons", []):
            lesson_title = lesson.get("lesson_title", "")
            content = lesson.get("content", "")
            entry = f"Chapter: {chap_title}\nLesson: {lesson_title}\nContent: {content}"
            texts.append(entry)
    return texts

def build_embeddings_index(texts, encoder_model_name=EMBEDDING_MODEL_NAME):
    model = SentenceTransformer(encoder_model_name)
    embeddings = model.encode(
        texts, show_progress_bar=False, convert_to_numpy=True, normalize_embeddings=True
    )
    dim = embeddings.shape[1]
    index = faiss.IndexFlatIP(dim)
    index.add(embeddings)
    return model, index, np.array(embeddings)

def retrieve_top_k(query, model, index, texts, k=TOP_K):
    q_emb = model.encode([query], convert_to_numpy=True, normalize_embeddings=True)
    D, I = index.search(q_emb, k)
    hits = []
    for idx in I[0]:
        if 0 <= idx < len(texts):
            text = texts[idx]
            if len(text) > 2000:
                text = text[:2000] + "..."
            hits.append(text)
    return hits

def call_groq_kimi_system(quiz_title, quiz_notes, retrieved_passages):
    system_message = {
        "role": "system",
        "content": (
            "You are an exam writer assistant. When given a quiz topic, supporting passages, "
            "and instructor notes, you MUST ONLY output a JSON object with the following schema exactly. "
            "Do NOT output anything else:\n\n"
            "{\n"
            '  "questions": [\n'
            '    {\n'
            '      "id": 1,\n'
            '      "text": "Question text here",\n'
            '      "options": ["Option 1 text", "Option 2 text", "Option 3 text", "Option 4 text"]\n'
            '    },\n'
            '    ...\n'
            '  ],\n'
            '  "answers": {\n'
            '    "1": "1", "2": "3", "3": "2", ...\n'
            '  }\n'
            "}\n\n"
            "Rules:\n"
            "1. Generate exactly 10 multiple-choice questions with 4 options each.\n"
            "2. In 'answers', the value must be the numeric index of the correct option (1,2,3,4) only.\n"
            "3. Do NOT include the word 'Option' in the answers.\n"
            "4. Do not add any text outside this JSON structure.\n"
            "5. Apply the instructor notes strictly when constructing the quiz "
            "(for example: control difficulty, style, or distribution of question types).\n"
        )
    }

    user_message = {
        "role": "user",
        "content": (
            f"Quiz Title: {quiz_title}\n\n"
            f"Instructor Notes: {quiz_notes}\n\n"
            f"Supporting Passages:\n\n" + "\n\n---\n\n".join(retrieved_passages)
        )
    }

    payload = {
        "model": GROQ_MODEL,
        "messages": [system_message, user_message],
        "temperature": 0.0,
        "max_tokens": 4000
    }

    headers = {
        "Authorization": f"Bearer {GROQ_API_KEY}",
        "Content-Type": "application/json"
    }

    resp = requests.post(GROQ_CHAT_ENDPOINT, headers=headers, json=payload, timeout=60)
    resp.raise_for_status()
    return resp.json()["choices"][0]["message"]["content"]

def generate_quiz(quiz_title, quiz_notes, model, index, texts):
    retrieved = retrieve_top_k(quiz_title, model, index, texts, k=TOP_K)
    if not retrieved:
        return {"error": "No relevant passages found"}

    raw_response = call_groq_kimi_system(quiz_title, quiz_notes, retrieved)

    try:
        json_text = raw_response.strip()
        if json_text.startswith("```"):
            parts = json_text.split("```")
            for p in parts:
                p_stripped = p.strip()
                if p_stripped.startswith("{"):
                    json_text = p_stripped
                    break

        parsed = json.loads(json_text)

        for q in parsed.get("questions", []):
            q["options"] = [opt.lstrip("0123456789. ").strip() for opt in q["options"]]

        numeric_answers = {}
        for qid, ans_value in parsed.get("answers", {}).items():
            ans_str = str(ans_value).strip()
            if ans_str not in ["1","2","3","4"]:
                ans_str = "1"  
            numeric_answers[qid] = ans_str
        parsed["answers"] = numeric_answers

    except Exception as e:
        return {"error": f"Failed to parse model output: {e}", "raw": raw_response}

    return parsed

def handle_client(conn, addr, model, index, texts):
    try:
        length_bytes = conn.recv(4)
        if not length_bytes:
            return
        length = struct.unpack(">I", length_bytes)[0]

        data = b""
        while len(data) < length:
            chunk = conn.recv(length - len(data))
            if not chunk:
                break
            data += chunk

        request = json.loads(data.decode("utf-8"))
        quiz_title = request.get("title")
        quiz_notes = request.get("notes", "")

        if not quiz_title:
            response = {"error": "Missing quiz title"}
        else:
            print(f"📝 Generating quiz for: {quiz_title}")
            response = generate_quiz(quiz_title, quiz_notes, model, index, texts)

            if "questions" in response and "answers" in response:
                print("\n=== QUIZ ===")
                for q in response["questions"]:
                    print(f"Q{q['id']}: {q['text']}")
                    for i, opt in enumerate(q["options"], start=1):
                        print(f"   {i}. {opt}")
                print("\n=== ANSWERS ===")
                for qid, ans in response["answers"].items():
                    print(f"Q{qid}: {ans}")
                print("=============\n")

        response_bytes = json.dumps(response, ensure_ascii=False).encode("utf-8")
        conn.sendall(struct.pack(">I", len(response_bytes)))
        conn.sendall(response_bytes)

    except Exception as e:
        print("Error handling client:", e)
    finally:
        conn.close()

def start_server():
    texts = load_dataset(DATASET_JSON)
    model, index, _ = build_embeddings_index(texts)

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_sock:
        server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_sock.bind((HOST, PORT))
        server_sock.listen(5)
        print(f"🚀 Quiz Generator Server listening on {HOST}:{PORT}")

        while True:
            conn, addr = server_sock.accept()
            print("Connected by", addr)
            handle_client(conn, addr, model, index, texts)

if __name__ == "__main__":
    if not GROQ_API_KEY:
        print("❌ Set GROQ_API_KEY environment variable first!")
        exit(1)
    start_server()