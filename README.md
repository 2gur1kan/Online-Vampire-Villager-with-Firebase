# 🧛 Online Vampire Villager

> An online Vampire-Villager game prototype built with Unity and Firebase.

Online Vampire Villager is a multiplayer game prototype developed with Unity and Firebase.

The main goal of this project was to explore how an online multiplayer game could be built using Firebase as the backend infrastructure, without requiring a dedicated game server.

The project focuses on experimenting with real-time multiplayer communication, player synchronization, game state management, and online session functionality.

---

## 🎮 About the Project

This project is a prototype inspired by the classic **Vampire / Werewolf / Villager** social deduction game concept.

Players join an online game and participate in a shared game session where the game state needs to be synchronized between multiple clients.

The project was developed primarily as a technical experiment to explore:

- Online multiplayer architecture
- Firebase as a backend solution
- Real-time data synchronization
- Player and game-state management
- Unity networking concepts
- Building an online game without maintaining a dedicated server

---

## ✨ Features

- 🌐 Online multiplayer prototype
- 🔥 Firebase backend integration
- ⚡ Real-time data synchronization
- 🎮 Unity-based gameplay
- 👥 Multiplayer game session management
- 🔄 Shared game-state synchronization
- ☁️ Cloud-based data management
- 💰 Server infrastructure with no dedicated game-server cost

> This project is a prototype and some systems may be incomplete or subject to change.

---

## ⚙️ Setup & Firebase Configuration / Kurulum ve Firebase Yapılandırması

### 🇬🇧 English
> **Note:** The default Firebase configuration file (`google-services-desktop.json` or `google-services.json`) has been removed from this repository for security reasons.

To run and test this project locally, you must provide your own Firebase configuration:

1. Create a new Firebase project at [Firebase Console](https://console.firebase.google.com/).
2. Enable **Realtime Database** (or Firestore / Auth depending on your setup).
3. Download your `google-services-desktop.json` (or `google-services.json`) config file from Project Settings.
4. Place the file inside the project directory:
   `Assets/StreamingAssets/google-services-desktop.json`
5. Build and run the project in Unity.

---

### 🇹🇷 Türkçe
> **Not:** Güvenlik gerekçeleriyle varsayılan Firebase yapılandırma dosyası (`google-services-desktop.json` veya `google-services.json`) bu depodan kaldırılmıştır.

Projeyi yerel ortamınızda çalıştırmak ve test etmek için kendi Firebase projenizi bağlamanız gerekmektedir:

1. [Firebase Console](https://console.firebase.google.com/) üzerinden yeni bir Firebase projesi oluşturun.
2. Projenizde **Realtime Database** servisini aktif edin.
3. Proje Ayarları (Project Settings) bölümünden `google-services-desktop.json` (veya `google-services.json`) dosyasını indirin.
4. Dosyayı projenin şu dizinine yerleştirin:
   `Assets/StreamingAssets/google-services-desktop.json`
5. Unity üzerinden projeyi çalıştırın.

---

## 🧠 Technical Approach

One of the main purposes of this project was experimenting with **Firebase as the backend infrastructure for an online multiplayer game**.

Instead of maintaining a traditional dedicated game server, Firebase is used to store and synchronize game-related information between players.

### Basic Architecture

```text
             ┌─────────────────┐
             │     Unity       │
             │   Game Client   │
             └────────┬────────┘
                      │
                      │
                      ▼
             ┌─────────────────┐
             │     Firebase    │
             │     Backend     │
             └────────┬────────┘
                      │
             ┌────────┴────────┐
             │                 │
             ▼                 ▼
        Player Data       Game State
             │                 │
             └────────┬────────┘
                      │
                      ▼
             ┌─────────────────┐
             │ Other Players   │
             │    Clients      │
             └─────────────────┘
