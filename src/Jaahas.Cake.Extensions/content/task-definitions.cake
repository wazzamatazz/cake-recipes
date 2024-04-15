// Task definitions as configured by the bootstrapper.
public readonly struct TaskDefinitions {

    public CakeTaskBuilder Clean { get; init; }

    public CakeTaskBuilder Restore { get; init; }

    public CakeTaskBuilder Build { get; init; }

    public CakeTaskBuilder Test { get; init; } 

    public CakeTaskBuilder Pack { get; init; }

    public CakeTaskBuilder BillOfMaterials { get; init; }

}
