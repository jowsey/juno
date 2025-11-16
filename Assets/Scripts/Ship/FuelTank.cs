namespace Ship
{
    public class FuelTank : BodyPart
    {
        public float StoredFuelKg;
        public float MaxFuelKg;

        public override void Reinitialise()
        {
            base.Reinitialise();

            StoredFuelKg = MaxFuelKg;
        }
    }
}