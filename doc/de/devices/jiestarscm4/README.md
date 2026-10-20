# JIESTAR – SCM 4

**JIESTAR – SCM 4** ist ein Bluetooth-Low-Energy-Modul (BLE), das bis zu vier Motoren ansteuert. Es können bis zu drei Module gleichzeitig gesteuert werden.

---
⚠️ Diese Geräte sind innerhalb desselben Modells nicht miteinander kompatibel, da sie identische BLE-Werbenachrichten verwenden. ⚠️

* [PowerBox – A Series](../powerboxaseries/README.md)
* [PowerBox – M Battery](../powerboxmbattery/README.md)
* [JIESTAR – SCM 4, Gerät 1](../jiestarscm4/README.md)
---

![JIESTAR – SCM 4](../../../../BrickController2/BrickController2/UI/Images/jiestarscm4_image.png)

## Kanäle

Jedes **JIESTAR – SCM 4**-Modul stellt vier Kanäle bereit:

| Kanäle | Zweck | Ausgabe |
|---|---|---|
| 1–4 | Anschlüsse A, B, C, D | -100 % bis +100 % |

## Erste Schritte

⚠️ **Das Modul wird bei der Suche nicht gefunden!** ⚠️

1. Öffne die Geräteliste, um das Gerät manuell hinzuzufügen.
2. Aktiviere in der Liste zum manuellen Hinzufügen **JIESTAR – SCM 4, Gerät 1**. Verwende für weitere Module **Gerät 2** und **Gerät 3**.
3. Schalte das **JIESTAR – SCM 4**-Modul ein. Nach dem Einschalten ist **Gerät 1** die Standardinstanz; die LED blinkt einmal pro Zyklus. Ändere die Instanz durch kurzes Drücken der Modultaste. Für **Gerät 2** blinkt die LED zweimal pro Zyklus, für **Gerät 3** dreimal.
4. Verwende das Gerät in einem Modell und weise seine Kanäle Controller-Aktionen zu.

## Hinweise

- Es werden bis zu drei **JIESTAR – SCM 4**-Module unterstützt.
- Die Ausgabe wird als Bluetooth-Werbenachricht gesendet. Daher meldet das Modul weder einen Verbindungsstatus noch die Akkuspannung.
- Die Verbindungsnachricht wird erneut gesendet, wenn alle Kanäle einige Sekunden lang auf null stehen.

## Siehe auch

- [Geräte](../../pages/device-list/README.md)
- [Controller-Aktion](../../pages/controller-action/README.md)
