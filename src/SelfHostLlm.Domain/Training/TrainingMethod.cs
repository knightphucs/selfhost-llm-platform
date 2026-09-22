namespace SelfHostLlm.Domain.Training;

/// <summary>Chỉ hỗ trợ LoRA/QLoRA — không full fine-tune.</summary>
public enum TrainingMethod
{
    LoRA,
    QLoRA,
}
