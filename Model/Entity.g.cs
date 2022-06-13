using System;
using SQLite;

namespace Metacrack.Model
{
    public class Entity0 : Entity { }
    public class Entity1 : Entity { }
    public class Entity2 : Entity { }
    public class Entity3 : Entity { }
    public class Entity4 : Entity { }
    public class Entity5 : Entity { }
    public class Entity6 : Entity { }
    public class Entity7 : Entity { }
    public class Entity8 : Entity { }
    public class Entity9 : Entity { }
    public class EntityA : Entity { }
    public class EntityB : Entity { }
    public class EntityC : Entity { }
    public class EntityD : Entity { }
    public class EntityE : Entity { }
    public class EntityF : Entity { }

    public partial class Entity
	{
        private static Type[] _types;

        static Entity()
        {
            _types = new Type[] {
                typeof(Entity0),
                typeof(Entity1),
                typeof(Entity2),
                typeof(Entity3),
                typeof(Entity4),
                typeof(Entity5),
                typeof(Entity6),
                typeof(Entity7),
                typeof(Entity8),
                typeof(Entity9),
                typeof(EntityA),
                typeof(EntityB),
                typeof(EntityC),
                typeof(EntityD),
                typeof(EntityE),
                typeof(EntityF),
            };
        }

        public static Type[] GetTypes()
        {
            return _types;
        }

        //This needs to move to a source generator
        public static Entity GetEntity(SQLiteConnection db, char hex, long rowId)
        {
            if (hex == '0') return db.Table<Entity0>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '1') return db.Table<Entity1>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '2') return db.Table<Entity2>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '3') return db.Table<Entity3>().Where(e => e.RowId == rowId).FirstOrDefault();

            if (hex == '4') return db.Table<Entity4>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '5') return db.Table<Entity5>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '6') return db.Table<Entity6>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '7') return db.Table<Entity7>().Where(e => e.RowId == rowId).FirstOrDefault();

            if (hex == '8') return db.Table<Entity8>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == '9') return db.Table<Entity9>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == 'A') return db.Table<EntityA>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == 'B') return db.Table<EntityB>().Where(e => e.RowId == rowId).FirstOrDefault();

            if (hex == 'C') return db.Table<EntityC>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == 'D') return db.Table<EntityD>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == 'E') return db.Table<EntityE>().Where(e => e.RowId == rowId).FirstOrDefault();
            if (hex == 'F') return db.Table<EntityF>().Where(e => e.RowId == rowId).FirstOrDefault();

            return db.Table<Entity0>().Where(e => e.RowId == rowId).FirstOrDefault();
        }

        public static Entity Create(char hex)
        {
            if (hex == '0') return new Entity0();
            if (hex == '1') return new Entity1();
            if (hex == '2') return new Entity2();
            if (hex == '3') return new Entity3();

            if (hex == '4') return new Entity4();
            if (hex == '5') return new Entity5();
            if (hex == '6') return new Entity6();
            if (hex == '7') return new Entity7();

            if (hex == '8') return new Entity8();
            if (hex == '9') return new Entity9();
            if (hex == 'A') return new EntityA();
            if (hex == 'B') return new EntityB();

            if (hex == 'C') return new EntityC();
            if (hex == 'D') return new EntityD();
            if (hex == 'E') return new EntityE();
            if (hex == 'F') return new EntityF();

            return new Entity0();
        }
    }
}

