# Unity Locale Generator

A Unity Editor extension that scans your Localization Tables and generates strongly-typed, static properties for each entry—making it easy and safe to access localized strings in code.

## Overview

**Unity Locale Generator** is an Editor‑only Unity package that watches your String Table Collections and automatically generates a C# class (`LocaleClasses.cs`) containing `LocalizedString` properties for each table entry. No more magic string keys—just `LocaleMyTable.MyKey`!

- ✅ **Auto‑generate** on table edits or via menu
- ✅ **Lazy initialization** for optimal performance
- ✅ **Configurable output folder** via Project Settings
- ✅ **Extension method** to clone `LocalizedString` instances 

---

## Features

- **Static classes per table**  
  Each table collection gets its own `Locale<TableName>` static class.

- **Lazy‑loaded `LocalizedString`**  
  Properties use `Lazy<LocalizedString>` under the hood, so they won’t be created until first accessed.

- **Clone extension**  
  A simple `.Clone()` extension on `LocalizedString` to create safe, mutable copies.

---

## Installation

Installing as GIT dependency via Package Manager
1. Open Package Manager (Window -> Package Manager)
2. Click `+` button on the upper left of the window, select "Add mpackage from git URL...'
3. Enter the following URL and click the `Add` button

   ```
   https://github.com/Mercury-Leo/UnityLocaleGenerator.git
   ```

## Usage

### 1. Configure Output Folder
Open Edit -> Project Settings -> Leo's Tools -> Locale Generator
Select a target folder for the generated file or leave it to 'Assets' as a default.

### 2. Generates Classes
* Manual
  In the Editor menu, select Tools -> Locale Generator -> Generate
* Automatic
  The package listens for
  * Table entries modification
  * Changes to the target folder

### 3. Access Localized strings
All your string tables appear as static classes named Locale<TableName>. Each key becomes a public property.

```csharp
private LocalizedString _data = LocaleTest.Data;
```
