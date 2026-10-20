# Mould King – MK 3.8

**Mould King – MK 3.8** ist ein Bluetooth-LE-Modul mit drei integrierten Motoren und LEGO-kompatiblen Kreuzachsen-Ausgängen sowie zwei elektrischen Mini-Steckverbindern.

![Mould King – MK 3.8](../../../../BrickController2/BrickController2/UI/Images/mk3_8_image.png)

## Kanäle

Das **Mould King – MK 3.8**-Modul stellt fünf Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1 | Linke Achse | -100 % bis +100 % |
| 2 | Stecker 1 | -100 % bis +100 % |
| 3 | Rechte Achse | -100 % bis +100 % |
| 4 | Stecker 2 | -100 % bis +100 % |
| 5 | Obere Achse | links / aus / rechts |

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **Mould King – MK 3.8 Device**.
3. Schalte das **Mould King – MK 3.8**-Modul ein.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
