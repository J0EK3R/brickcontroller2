# Mould King – MK 3.0

**Mould King – MK 3.0** ist ein Bluetooth-LE-Modul mit zwei integrierten Motoren und LEGO-kompatiblen Kreuzachsen-Ausgängen.

![Mould King – MK 3.0](../../../../BrickController2/BrickController2/UI/Images/mk3_image.png)

## Kanäle

Das **Mould King – MK 3.0**-Modul stellt vier Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1 | Linke Achse | -100 % bis +100 % |
| 2 | Rechte Achse | -100 % bis +100 % |
| 3 | Interne Funktion 1 | Aus / Ein |
| 4 | Interne Funktion 2 | Aus / Ein |

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **Mould King – MK 3.0 Device**.
3. Schalte das **Mould King – MK 3.0**-Modul ein.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
