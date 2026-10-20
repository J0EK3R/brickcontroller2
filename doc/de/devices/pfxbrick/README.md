# PFx Brick

PFx Brick (FX Bricks) ist ein Bluetooth-LE-Brick, der Power-Functions-Motoren und -Leuchten steuert sowie Geräusche abspielt.

![PFx Brick](../../../../BrickController2/BrickController2/UI/Images/pfxbrick_image.png)

## Kanäle

Das Gerät stellt zehn Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| A–B | Power-Functions-Motoren | -100 % bis +100 % |
| 1–8 | Beleuchtung | Helligkeit von 0 bis 100 % |

## Erste Schritte

1. Schalte den PFx Brick ein.
2. Öffne die Geräteliste und suche nach Geräten.
3. Wähle den PFx Brick in den Suchergebnissen aus, um ihn hinzuzufügen.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Geräusche

Geräusche steuerst du über Makros, die du Controller-Aktionen zuweisen kannst:

- **Geräusch abspielen** startet eine auf dem Gerät gespeicherte Audiodatei.
- **Geräusch stoppen** beendet die Wiedergabe einer Audiodatei.
- **Geräusch umschalten** startet die Wiedergabe oder beendet sie, wenn sie bereits läuft.
- **Lautstärke festlegen** stellt die gewünschte Lautstärke ein.
- **Lautstärke erhöhen** und **Lautstärke verringern** ändern die Lautstärke schrittweise.

## Skripte

Skriptmakros kannst du Controller-Aktionen zuweisen:

- **Skript ausführen** startet ein auf dem Gerät gespeichertes Skript.
- **Skript stoppen** beendet ein laufendes Skript.

## Einstellungen

- **Standardlautstärke**: Lautstärke nach dem Verbindungsaufbau. Der Standardwert ist 50 %.

## Hinweise

- Bei der ersten Verwendung verbindet sich das Gerät nicht automatisch. Stelle die Verbindung über die Geräteseite oder durch Starten eines Modells her, das dieses Gerät verwendet.
- Audiodateien und Skripte müssen auf dem PFx Brick vorhanden sein; die App löst sie lediglich aus.
- Audiodateien und Skripte verwaltest du mit der PFx-Brick-App, nicht mit dieser App.

## Siehe auch

- [Dokumentation von FX Bricks](https://shop.fxbricks.com/pages/documentation)
- [Controller-Aktion](../../pages/controller-action/README.md)
