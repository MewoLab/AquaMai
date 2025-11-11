namespace AquaMai.Config.Types;

// The enum values are the column indices in a 8x8 float BE matrix in Mai2.acf file
// It's NOT consistent with WASAPI's channel indices (bits in dwChannelMask).
public enum SoundChannel
{
    P1SpeakerLeft = 0, // Routes to WASAPI's SPEAKER_FRONT_LEFT (0x1 = 1 << 0)
    P1SpeakerRight = 1, // Routes to WASAPI's SPEAKER_FRONT_RIGHT (0x2 = 1 << 1)
    P1HeadphoneLeft = 2, // Routes to WASAPI's SPEAKER_FRONT_CENTER (0x4 = 1 << 2)
    P1HeadphoneRight = 3, // Routes to WASAPI's SPEAKER_LOW_FREQUENCY (0x8 = 1 << 3)
    P2SpeakerLeft = 6, // Routes to WASAPI's SPEAKER_BACK_LEFT (0x10) = 1 << 4)
    P2SpeakerRight = 7, // Routes to WASAPI's SPEAKER_BACK_RIGHT (0x20) = 1 << 5)
    P2HeadphoneLeft = 4, // Routes to WASAPI's SPEAKER_SIDE_LEFT (0x200 = 1 << 9)
    P2HeadphoneRight = 5, // Routes to WASAPI's SPEAKER_SIDE_RIGHT (0x400 = 1 << 10)
    None = 8,
}
