#include <move.h>
#include <vector>
#include <set>

struct MoveWorld
{
	b2World b2;
	std::vector<b2Body*> bodies;

	MoveWorld() : b2({}) {}
};

MoveWorld* move_new()
{
	return new MoveWorld;
}

void move_delete(MoveWorld* world)
{
	delete world;
}

MoveUnit& get_unit(void* units, int unitSizeInBytes, int i)
{
	return *reinterpret_cast<MoveUnit*>(reinterpret_cast<std::byte*>(units) + i * unitSizeInBytes);
}

b2Body* create_body(b2World& b2, const MoveUnit& unit, size_t i)
{
	b2CircleShape shape;
	shape.m_radius = unit.radius;

	b2BodyDef bd;
	bd.fixedRotation = true;
	bd.type = b2_dynamicBody;
	bd.position = unit.position;

	b2FixtureDef fd;
	fd.shape = &shape;
	fd.friction = 0;
	fd.restitutionThreshold = FLT_MAX;
	fd.density = 1.0f / (b2_pi * unit.radius * unit.radius);

	auto body = b2.CreateBody(&bd);
	auto fixture = body->CreateFixture(&fd);
	fixture->GetUserData().pointer = i;
	return body;
}

void move_step(MoveWorld* world, void* units, int unitsLength, int unitSizeInBytes, float dt)
{
	auto& bodies = world->bodies;
	auto& b2 = world->b2;

	for (auto i = 0; i < unitsLength; i ++) {
		auto& unit = get_unit(units, unitSizeInBytes, i);
		if (i >= bodies.size()) {
			bodies.push_back(create_body(b2, unit, i));
		}
		bodies[i]->ApplyForceToCenter(unit.force, unit.force.x != 0 || unit.force.y != 0);
	}

	b2.Step(dt, 8, 3);

	for (auto i = 0; i < unitsLength; i++) {
		auto& unit = get_unit(units, unitSizeInBytes, i);
		auto body = bodies[i];
		unit.position = body->GetPosition();
		unit.velocity = body->GetLinearVelocity();
		unit.state &= ~MOVE_IN_CONTACT;
	}

	auto contact = b2.GetContactList();
	while (contact != nullptr)
	{
		if (contact->IsEnabled() && contact->IsTouching()) {
			auto a = contact->GetFixtureA()->GetUserData().pointer;
			auto b = contact->GetFixtureB()->GetUserData().pointer;
			get_unit(units, unitSizeInBytes, a).state |= MOVE_IN_CONTACT;
			get_unit(units, unitSizeInBytes, b).state |= MOVE_IN_CONTACT;
		}
		contact = contact->GetNext();
	}
}

struct MoveQueryCallback : b2QueryCallback
{
	int* begin;
	int* end;

	virtual bool ReportFixture(b2Fixture* fixture)
	{
		if (begin == end)
			return false;

		*begin++ = fixture->GetUserData().pointer;
		return true;
	}
};

int move_query_aabb(MoveWorld* world, b2AABB* aabb, int* units, int unitsLength)
{
	MoveQueryCallback cb;
	cb.begin = units;
	cb.end = units + unitsLength;

	world->b2.QueryAABB(&cb, *aabb);
	return cb.end - cb.begin;
}

struct MoveRayCastCallback : b2RayCastCallback
{
	int* unit;
	int result;

	virtual float ReportFixture(b2Fixture* fixture, const b2Vec2& point, const b2Vec2& normal, float fraction)
	{
		*unit = fixture->GetUserData().pointer;
		result = 1;
		return 0;
	}
};

int move_raycast(MoveWorld* world, b2Vec2* a, b2Vec2* b, int* unit)
{
	MoveRayCastCallback cb;
	cb.result = 0;
	cb.unit = unit;

	world->b2.RayCast(&cb, *a, *b);
	return cb.result;
}
