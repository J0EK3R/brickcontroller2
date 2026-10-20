# BuWizz 3

BuWizz 3 (BuWizz 3.0 Pro) ist ein Bluetooth-LE-Akku-Brick mit vier Powered-Up-Anschlüssen und zwei Power-Functions-Anschlüssen.

![BuWizz 3](../../../../BrickController2/BrickController2/UI/Images/buwizz3_image.png)

## Kanäle

Das Gerät stellt sechs Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1–4 | Powered-Up-Motoren | -100 % bis +100 %, Servo oder Schrittmotor |
| A, B | Power-Functions-Motoren | -100 % bis +100 % |

## Erste Schritte

1. Schalte den BuWizz 3 ein.
2. Öffne die Geräteliste und tippe auf **Nach neuen Geräten suchen**. Gefundene Geräte werden zur Liste hinzugefügt.
3. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Strombegrenzung

Jeder Anschluss hat eine eigene Strombegrenzung, die du in den Geräteeinstellungen ändern kannst. Standardwerte (wie in der offiziellen BuWizz-App):

| Anschlüsse | Standardbegrenzung |
|---|---|
| 1–4 (Powered Up) | 1050 mA |
| A, B (Power Functions) | 2100 mA |

## Ruhemodus

Im Ruhemodus wird der Akku vom Gerät getrennt, zum Beispiel zur Aufbewahrung oder für den Transport. Du kannst den Modus bei bestehender Verbindung auf der Geräteseite aktivieren; die App bittet dich vorher um Bestätigung. Um das Gerät wieder einzuschalten, musst du normalerweise das Ladegerät anschließen.

## Hinweise

- Die Ausgabetypen Servo und Schrittmotor sind nur an den Powered-Up-Anschlüssen 1–4 verfügbar.
- Die Akkuspannung wird bei bestehender Verbindung auf der Geräteseite angezeigt.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
