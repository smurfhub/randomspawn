# RandomSpawn

Mod de Valheim para BepInEx que elige un punto aleatorio cerca de una estructura de Meadows cuando vanilla no tiene una cama o un punto de logout aplicable.

## Qué hace

- Elige un punto a entre 5 y 10 m de una estructura de Meadows distinta de `StartTemple`.
- Espera a que la zona esté lista y comprueba el terreno en el centro y en cuatro puntos alrededor. Descarta pendientes con más de 0,5 m de variación.
- Comprueba el espacio con la cápsula del prefab del jugador. Descarta obstáculos y jugadores que ya estén en el punto.
- Tras validar el candidato, espera a que se cargue el área activa del cliente antes de completar el spawn.
- Descarta un candidato si su carga o validación supera 60 segundos. Abandona la búsqueda tras 180 segundos en total.
- Prueba hasta 10 candidatos. Si no hay estructuras de Meadows o ninguno es válido, usa el inicio vanilla.
- Reinicia el estado temporal al inicializarse un mundo nuevo.

## Qué no hace

- No cambia `StartTemple`, su posición ni su icono del mapa.
- No modifica el terreno ni las estructuras.
- No guarda candidatos ni reserva posiciones. Solo detecta jugadores presentes al validar el punto, por lo que dos entradas simultáneas aún podrían coincidir.
- No garantiza detectar cualquier pendiente abrupta o construcción.
- No guarda el candidato como dato propio; al completar el spawn, vanilla puede actualizar el punto de hogar del perfil.

## Cuándo se usa vanilla

- Si hay una cama registrada, Valheim sigue su ruta de cama.
- Si hay un punto de logout que vanilla va a usar, al entrar normalmente reaparece allí.
- Si no hay estructuras de Meadows o ninguno de los 10 candidatos resulta válido, usa el inicio vanilla.
- Si la búsqueda supera los 180 segundos, abandona y usa vanilla.

## Instalación

1. Instala [BepInEx](https://github.com/BepInEx/BepInEx) para Valheim.
2. Copia `RandomSpawn.dll` en `BepInEx/plugins`.
3. En multijugador, instala el mod en cada cliente que quiera usarlo. No hace falta en un servidor dedicado.
