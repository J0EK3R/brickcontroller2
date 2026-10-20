# PowerBox – M Battery

**PowerBox – M Battery** ist ein Bluetooth-LE-Motormodul.

---
⚠️ Diese Geräte sind innerhalb desselben Modells nicht miteinander kompatibel, da sie identische BLE-Werbenachrichten verwenden. ⚠️

* [PowerBox – A Series](../powerboxaseries/README.md)
* [PowerBox – M Battery](../powerboxmbattery/README.md)
* [JIESTAR – SCM 4, Gerät 1](../jiestarscm4/README.md)
---

![PowerBox – M Battery](../../../../BrickController2/BrickController2/UI/Images/powerboxmbattery_image.png)

## Kanäle

Das **PowerBox – M Battery**-Modul stellt einen Kanal bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1 | Motor A | -100 % bis +100 % |

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **PowerBox – M Battery Device**.
3. Schalte das **PowerBox – M Battery**-Modul ein.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
