function getRandomInt(min, max) {
	min = Math.ceil(min);
	max = Math.floor(max);
	return Math.floor(Math.random() * (max - min + 1)) + min;
}

const intervalDelay = 3000;
const transitionSpeed = 2;

const slider = document.querySelector(".slider input");
const img = document.querySelector(".images .img-2");
const dragLine = document.querySelector(".slider .drag-line");

img.style.transition = `width ${transitionSpeed}s`;
dragLine.style.transition = `left ${transitionSpeed}s`;

const slide = () => {
	const newValue = getRandomInt(0, 100);
	dragLine.style.left = newValue + "%";
	img.style.width = newValue + "%";
	slider.value = newValue;
};
let inter = setInterval(slide, intervalDelay);
slider.oninput = () => {
	clearInterval(inter);
	img.style.transition = 'none';
	dragLine.style.transition = 'none';
	const sliderVal = slider.value;
	dragLine.style.left = sliderVal + "%";
	img.style.width = sliderVal + "%";
};
slider.addEventListener('mouseup', () => {
	img.style.transition = `width ${transitionSpeed}s`;
	dragLine.style.transition = `left ${transitionSpeed}s`;
	inter = setInterval(slide, intervalDelay);
});