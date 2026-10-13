# EchoGame - Unity Project Guidelines

## Proyecto y Configuración
- **Motor**: Unity 6 (`6000.6.5f1`)
- **Render Pipeline**: Universal Render Pipeline (URP 17.6)
- **Sistema de Input**: Nuevo Unity Input System (`com.unity.inputsystem`, acciones en `Assets/InputSystem_Actions.inputactions`)

## Convenciones de Código C#
- Organizar los scripts dentro de `Assets/Scripts/<Modulo>/` (por ejemplo: `Assets/Scripts/Player/`, `Assets/Scripts/Core/`, etc.).
- Preferir campos privados serializados con atributo: `[SerializeField] private float _moveSpeed = 5f;`.
- Usar APIs modernas de Unity 6:
  - Usar `FindFirstObjectByType<T>()` o `FindAnyObjectByType<T>()` en lugar del obsoleto `FindObjectOfType<T>()`.
  - Aprovechar el nuevo Input System mediante callbacks o eventos (`PlayerInput` o referencias directas a `InputActionAsset`).
- Mantener scripts modulares y desacoplados (Single Responsibility Principle).
