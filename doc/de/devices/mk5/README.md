# Mould King – MK 5.0

**Mould King – MK 5.0** ist ein Bluetooth-LE-Modul mit drei integrierten Motoren und LEGO-kompatiblen Kreuzachsen-Ausgängen.

![Mould King – MK 5.0](../../../../BrickController2/BrickController2/UI/Images/mk5_image.png)

## Kanäle

Das **Mould King – MK 5.0**-Modul stellt fünf Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1 | Motoren A+B | -100 % bis +100 % |
| 2 | Motor T | -100 % bis +100 % |
| 3 | Schusseffekt | Aus / Ein |
| 4 | Motoren A↔B+T | -100 % bis +100 % |
| 5 (virtuell) | Motor T sperren: A↔B | Aus / Ein |

Kanal 5 ist ein virtueller Kanal. Wenn er aktiviert ist, ändert er das Verhalten von Kanal 4: Motoren A und B drehen sich dann in entgegengesetzte Richtungen, während Motor T unverändert bleibt.

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **Mould King – MK 5.0 Device**.
3. Schalte das **Mould King – MK 5.0**-Modul ein.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
