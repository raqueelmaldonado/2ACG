#pragma once

#include <glm/vec3.hpp>
#include <glm/vec4.hpp>
#include <glm/matrix.hpp>

#include "../framework/camera.h"
#include "mesh.h"
#include "texture.h"
#include "shader.h"

// --- Lab 4 Libraries ---
#include "openvdbReader.h"
#include "bbox.h"

class Material {
public:

	Shader* shader = NULL;
	Texture* texture = NULL;
	glm::vec4 color;

	virtual void setUniforms(Camera* camera, glm::mat4 model) = 0;
	virtual void render(Mesh* mesh, glm::mat4 model, Camera* camera) = 0;
	virtual void renderInMenu() = 0;
	virtual void loadVDB(std::string file_path) {};
};

class FlatMaterial : public Material {
public:

	FlatMaterial(glm::vec4 color = glm::vec4(1.f));
	~FlatMaterial();

	void setUniforms(Camera* camera, glm::mat4 model);
	void render(Mesh* mesh, glm::mat4 model, Camera* camera);
	void renderInMenu();
};

class WireframeMaterial : public FlatMaterial {
public:

	WireframeMaterial();
	~WireframeMaterial();

	void render(Mesh* mesh, glm::mat4 model, Camera* camera);
};

class StandardMaterial : public Material {
public:

	bool first_pass = false;

	bool show_normals = false;
	Shader* base_shader = NULL;
	Shader* normal_shader = NULL;

	StandardMaterial(glm::vec4 color = glm::vec4(1.f));
	~StandardMaterial();

	void setUniforms(Camera* camera, glm::mat4 model);
	void render(Mesh* mesh, glm::mat4 model, Camera* camera);
	void renderInMenu();
};
//extend the material class:

class VolumeMaterial : public Material {
public:
	float absorption = 0.01; // Attribute for the absorption rate in volume rendering

	VolumeMaterial();
	~VolumeMaterial();

	void setUniforms(Camera* camera, glm::mat4 model) override;
	void renderInMenu() override; // For GUI control in ImGui
	void render(Mesh* mesh, glm::mat4 model, Camera* camera) override;

	// Lab 4 Functions
	void loadVDB(std::string file_path) override;
	void estimate3DTexture(easyVDB::OpenVDBReader* vdbReader);


	// Lab 3 attributes
	int volume_type;
	int shader_type;
	float step_size = 0.01;
	float noise_scale = 0.01;
	int noise_detail = 1;

	// Lab 4 attributes
	int volume_data_idx;
	float density = 0.5;
	std::string vdb_path;


};