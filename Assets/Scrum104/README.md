# SCRUM-104 – Pause menu a nastavenia zvuku

Unity 6000.6.6f1. Základný rozsah SCRUM-121, 122, 123 a 124.
SCRUM-129 (oddelená hlasitosť hudby a efektov) nie je implementovaný.

## Vyskúšanie

1. Otvor `Assets/Scrum104/Generated/PauseMenuDemo.unity`.
2. Stlač Play. Čas a žltý štvorček sa pohybujú, hudba hrá v slučke.
3. Klikni na Pauza. Čas aj pohyb sa zastavia, menu zostane ovládateľné.
4. Zmeň hlasitosť. Jedna hodnota ovláda hudbu aj kliknutia; 0 znamená stlmenie.
5. Klikni na Pokračovať. Čas sa obnoví a zaznie kliknutie podľa hlasitosti.
6. Ukonči a znova spusti Play, prípadne reštartuj aplikáciu: hodnota sa obnoví.

## Integrácia pre Erika a Andreja

- Do hernej scény vlož prefab `Generated/PauseMenu.prefab` iba raz.
- Scéna potrebuje jeden EventSystem s InputSystemUIInputModule a aktívny AudioListener.
- Prefab má vlastný Canvas a dočasné tlačidlo Pauza. Pri použití spoločného HUD
  v inštancii prefabu zmeň referenciu `pauseButton` na spoločné HUD tlačidlo
  a vypni pôvodné dieťa PauseButton. Controller pripojí otvorenie menu sám
  pri Awake; nepridávaj na to isté tlačidlo ďalší rovnaký listener.
  Iné udalosti môžu priamo volať `OpenPauseMenu()`.
- Tlačidlá menu sú už pripojené. Ďalšie UI tlačidlá môžu volať `PlayClick()`.
- Hudba: Assets/Audio/Music/hudba_hry.wav; klik: Assets/Audio/SFX/kliknutie.wav.
  Tieto pôvodné assety musia byť dostupné aj v integračnej vetve.
- Hlasitosť je uložená cez PlayerPrefs pod kľúčom
  `ParadiseTycoon.Audio.MasterVolume`, predvolená hodnota je 70 %.
- Zastavenie používa Time.timeScale = 0. Pokračovanie vráti predchádzajúcu hodnotu.
  Herné systémy používajúce unscaledDeltaTime alebo vlastný čas musia rešpektovať
  `IsPaused`. Ďalší systém pauzy musí byť koordinovaný s týmto controllerom.
- Hudba a UI používajú vlastné AudioSource na koreni prefabu, mimo vypínaného panelu.
  Hudba sa spúšťa iba pri inicializácii, otvorenie menu ju nereštartuje.
- Prefab nepretrváva medzi scénami: vlož ho do scény, kde je potrebný. Druhá
  inštancia v rovnakej scéne sa automaticky deaktivuje, aby sa hudba nezdvojila.
- Existujúca SampleScene ani Build Settings sa nemenia. Testovacia scéna obsahuje
  vlastnú kameru, listener a EventSystem; tieto demo objekty do hlavnej scény nekopíruj.

## Súbory

Runtime/PauseMenuController.cs – pauza, hlasitosť, hudba, kliknutia.
Runtime/DemoClock.cs – iba testovací ukazovateľ času a pohybu.
Runtime/PauseMenuSmoke.cs – overenie cez explicitný parameter editora, mimo player buildu.
Editor/Scrum104Builder.cs – Tools > SCRUM-104 > Create demo and prefab.
Pozor: generovanie vytvorí novú demo scénu a prepíše vygenerovaný prefab a demo scénu.
Pred použitím ulož rozpracovanú scénu; nepoužívaj na ručne upravený prefab.
