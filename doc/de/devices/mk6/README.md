# Mould King – MK 6.0

**Mould King – MK 6.0** ist ein Bluetooth-LE-Modul, das bis zu sechs Motoren ansteuert. Es können bis zu drei Module gleichzeitig gesteuert werden.

![Mould King – MK 6.0](../../../../BrickController2/BrickController2/UI/Images/mk6_image.png)

## Kanäle

Jedes **Mould King – MK 6.0**-Modul stellt sechs Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1–6 | Anschlüsse A, B, C, D, E, F | -100 % bis +100 % |

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **Mould King – MK 6.0 Device 1**. Verwende für weitere Module **Device 2** und **Device 3**.
3. Schalte das **Mould King – MK 6.0**-Modul ein. Nach dem Einschalten ist **Device 1** die Standardinstanz; die LED blinkt einmal pro Zyklus. Ändere die Instanz durch kurzes Drücken der Modultaste. Für **Device 2** blinkt die LED zweimal pro Zyklus, für **Device 3** dreimal.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Es werden bis zu drei **Mould King – MK 6.0**-Module unterstützt.
- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
