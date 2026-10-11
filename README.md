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

## ⚙️ Setup & Firebase Configuration

This project uses **Firebase Realtime Database** for online multiplayer communication. To run it, you must connect **your own** Firebase project. No server information is included in this repository.

> [!IMPORTANT]
> Three things must all point to the **same** Firebase project:
> 1. the config file (`google-services.json`)
> 2. the Android package name
> 3. the database URL written in `FireBaseDataBase.cs`

<details open>
<summary><b>🇬🇧 English</b></summary>

### Setup

1. Create a project in the [Firebase Console](https://console.firebase.google.com/) and enable **Realtime Database**.
2. Copy your database URL from the top of the Realtime Database page.
3. Go to **Project settings → General → Your apps** and add an **Android app**. Its package name must exactly match Unity's: `Edit → Project Settings → Player → Other Settings → Package Name`.
4. Download `google-services.json` and place it at:
   ```
   Assets/google-services.json
   ```
5. Open `FireBaseDataBase.cs`, find this line in `Awake()` and replace the URL with your own:
   ```csharp
   app.Options.DatabaseUrl = new Uri("YOUR_FIREBASE_DATABASE_URL");
   ```
6. Set your database [Rules](#database-rules).
7. Uninstall any old build from your phone, then rebuild and install.

> [!NOTE]
> `Assets/StreamingAssets/google-services-desktop.json` is generated **automatically** by the Firebase Unity SDK for the Unity Editor. You do not need to create it manually.

> [!WARNING]
> Replacing `google-services.json` does **not** update the URL hardcoded in `FireBaseDataBase.cs`. Change it manually.

### Troubleshooting

Works in the Unity Editor but not on a phone, or the lobby is created but nothing appears in Firebase? Check:

- [ ] There is only **one** `google-services.json` in the project (search `google-services` in the Project window).
- [ ] `package_name` inside `google-services.json` is identical to Unity's Package Name (case, `_` and `.` included).
- [ ] `project_id` and `firebase_url` in `google-services.json` belong to your current project.
- [ ] The URL in `FireBaseDataBase.cs` matches your Realtime Database URL exactly.
- [ ] If you switched projects, delete `Assets/StreamingAssets/google-services-desktop.json` and `Assets/Plugins/Android/FirebaseApp.androidlib` (if present); they are regenerated automatically.
- [ ] Your Rules allow the write. Denied writes fail **silently** unless you log them.
- [ ] You uninstalled the old app and rebuilt. If needed, delete the `Library` folder.

To see write errors, log the result, for example in `CreateRoom`:

```csharp
roomRef.SetRawJsonValueAsync(JsonUtility.ToJson(roomObj)).ContinueWith(task =>
{
    if (task.IsFaulted)
        Debug.LogError("Room write ERROR: " + task.Exception);
});
```

On Android, run `adb logcat -s Unity`. A `Permission denied` message means the Rules are blocking the write.

### Database Rules

Configure your Realtime Database rules according to your authentication and access requirements. For **local testing only**, you may temporarily allow open access:

```json
{ "rules": { ".read": true, ".write": true } }
```

</details>

<details>
<summary><b>🇹🇷 Türkçe</b></summary>

### Kurulum

1. [Firebase Console](https://console.firebase.google.com/) üzerinden bir proje oluşturun ve **Realtime Database** servisini etkinleştirin.
2. Realtime Database sayfasının en üstündeki veritabanı URL'inizi kopyalayın.
3. **Project settings → General → Your apps** bölümünden bir **Android uygulaması** ekleyin. Paket adı Unity'deki ile birebir aynı olmalı: `Edit → Project Settings → Player → Other Settings → Package Name`.
4. `google-services.json` dosyasını indirip şu konuma koyun:
   ```
   Assets/google-services.json
   ```
5. `FireBaseDataBase.cs` dosyasını açın, `Awake()` içindeki şu satırı bulup URL'i kendi adresinizle değiştirin:
   ```csharp
   app.Options.DatabaseUrl = new Uri("YOUR_FIREBASE_DATABASE_URL");
   ```
6. Veritabanı [kurallarını](#veritabanı-kuralları-rules) ayarlayın.
7. Telefondaki eski uygulamayı silin, ardından yeniden build alıp kurun.

> [!NOTE]
> `Assets/StreamingAssets/google-services-desktop.json` dosyasını Firebase Unity SDK, Unity Editor için **otomatik** üretir. Elle oluşturmanıza gerek yoktur.

> [!WARNING]
> `google-services.json` dosyasını değiştirmek, `FireBaseDataBase.cs` içindeki sabit URL'i **güncellemez**. Elle değiştirmeniz gerekir.

### Sorun Giderme

Unity Editor'de çalışıp telefonda çalışmıyor mu, ya da lobi kuruluyor ama Firebase'e hiçbir şey düşmüyor mu? Kontrol edin:

- [ ] Projede yalnızca **bir** `google-services.json` var (Project penceresinde `google-services` aratın).
- [ ] `google-services.json` içindeki `package_name`, Unity'deki Package Name ile aynı (büyük/küçük harf, `_` ve `.` dahil).
- [ ] `google-services.json` içindeki `project_id` ve `firebase_url` mevcut projenize ait.
- [ ] `FireBaseDataBase.cs` içindeki URL, Realtime Database URL'inizle birebir aynı.
- [ ] Proje değiştirdiyseniz `Assets/StreamingAssets/google-services-desktop.json` ve varsa `Assets/Plugins/Android/FirebaseApp.androidlib` dosyalarını silin; otomatik yeniden üretilirler.
- [ ] Rules yazmaya izin veriyor. Reddedilen yazma, loglamazsanız **sessizce** başarısız olur.
- [ ] Eski uygulamayı silip yeniden build aldınız. Gerekirse `Library` klasörünü silin.

Yazma hatalarını görmek için sonucu loglayın, örneğin `CreateRoom` içinde:

```csharp
roomRef.SetRawJsonValueAsync(JsonUtility.ToJson(roomObj)).ContinueWith(task =>
{
    if (task.IsFaulted)
        Debug.LogError("Oda yazma HATASI: " + task.Exception);
});
```

Android'de `adb logcat -s Unity` komutunu çalıştırın. `Permission denied` mesajı, Rules'un yazmayı engellediği anlamına gelir.

### Veritabanı Kuralları (Rules)

Realtime Database kurallarını kimlik doğrulama ve erişim ihtiyaçlarınıza göre yapılandırın. Yalnızca **yerel test için** geçici olarak açık erişim verebilirsiniz:

```json
{ "rules": { ".read": true, ".write": true } }
```

</details>


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
