# RandomSpawn

Mod de Valheim para BepInEx que elige un punto de respawn aleatorio cerca de una estructura de Meadows.

## Qué hace

- Si el personaje tiene cama o punto de logout, Valheim los usa como siempre.
- En caso contrario, el mod elige una estructura de Meadows y un punto a un máximo de 5 m de ella.
- Espera a que la zona del respawn y el área activa del cliente estén cargadas.
- Ajusta la altura para que el jugador aparezca sobre el suelo.
- Si no encuentra estructuras válidas, usa el inicio vanilla.
- No modifica `StartTemple`, su icono del mapa ni guarda posiciones de respawn.

## Instalación

1. Instala [BepInEx](https://github.com/BepInEx/BepInEx) para Valheim.
2. Copia `RandomSpawn.dll` en `BepInEx/plugins`.
3. En multijugador, instálalo en el servidor y en los clientes.
