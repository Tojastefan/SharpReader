function getRandomInt(min, max) {
	min = Math.ceil(min);
	max = Math.floor(max);
	return Math.floor(Math.random() * (max - min + 1)) + min;
}
const slider = document.querySelector(".slider input");
const img = document.querySelector(".images .img-2");
const dragLine = document.querySelector(".slider .drag-line");
let inner = undefined;
const slide = () => {
	clearInterval(inner);
	const newValue = getRandomInt(0, 100);
	let sliderVal = slider.value;
	if (sliderVal != newValue) {
		inner = setInterval(() => {
			if (sliderVal > newValue) {
				--sliderVal;
				if (sliderVal < newValue)
					clearInterval(inner);
			} else {
				++sliderVal;
				if (sliderVal > newValue)
					clearInterval(inner);
			}
			dragLine.style.left = sliderVal + "%";
			img.style.width = sliderVal + "%";
			slider.value = sliderVal;
		}, 10);
	}
};
let inter = setInterval(slide, 2000);
slider.oninput = () => {
	clearInterval(inner);
	clearInterval(inter);
	const sliderVal = slider.value;
	dragLine.style.left = sliderVal + "%";
	img.style.width = sliderVal + "%";
};
slider.addEventListener('mouseup', () => {
	inter = setInterval(slide, 2000);
});